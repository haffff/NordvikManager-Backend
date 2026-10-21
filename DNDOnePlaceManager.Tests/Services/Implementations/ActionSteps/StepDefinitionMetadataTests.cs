using DNDOnePlaceManager.Enums;
using DNDOnePlaceManager.Services.Implementations.ActionSteps;
using System;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Xunit;

namespace DNDOnePlaceManager.Tests.Services.Implementations.ActionSteps
{
    // The action editor renders entirely from this metadata (argument widgets, variable
    // autocomplete, step summaries), so a typo here silently degrades the editor.
    public class StepDefinitionMetadataTests
    {
        // Summary/DataType are constant expression-bodied members, so the steps don't need
        // their constructor dependencies (IServiceScopeFactory etc.) to be inspected.
        private static IActionStepDefinition[] AllSteps() =>
            typeof(IActionStepDefinition).Assembly.GetTypes()
                .Where(t => typeof(IActionStepDefinition).IsAssignableFrom(t) && !t.IsAbstract && !t.IsInterface)
                .Select(t => (IActionStepDefinition)RuntimeHelpers.GetUninitializedObject(t))
                .ToArray();

        private static ActionDefinitionArgumentLookup Arg(string stepValue, string argName)
        {
            var step = AllSteps().Single(s => s.Value == stepValue);
            var def = StepDefinitionMetadata.Build(step);
            return new ActionDefinitionArgumentLookup(def, def.Arguments.Single(a => a.Name == argName));
        }

        private record ActionDefinitionArgumentLookup(DNDOnePlaceManager.Models.ActionDefinitionResponse Step, DNDOnePlaceManager.Models.ActionDefinitionArgument Arg);

        // ── What a step gives back: the editor shows it under the step and in the variables panel ──

        [Fact]
        public void EveryOutputArgument_DeclaresWhatItHolds()
        {
            var missing = AllSteps()
                .SelectMany(s => StepDefinitionMetadata.Build(s).Arguments.Where(a => a.IsOutput && a.Output == null).Select(a => $"{s.Value}.{a.Name}"))
                .ToList();

            Assert.Empty(missing);
        }

        [Fact]
        public void Find_ListsItsFieldsPerKind_AndFirstGivesOne()
        {
            var output = Arg("Find", "Output").Arg.Output;

            Assert.Equal("list", output.Kind);
            Assert.Equal("What", output.FieldsBy);
            Assert.Contains("cardId", output.FieldsByValue["tokens"]);
            Assert.Contains("isTemplate", output.FieldsByValue["cards"]);
            Assert.Equal("First", output.SingleWhen);
        }

        [Fact]
        public void RollDice_IsAnObject_OrAValueWithSimpleOutput()
        {
            var output = Arg("RollDice", "OutputVariable").Arg.Output;

            Assert.Equal("object", output.Kind);
            Assert.Contains("result", output.Fields);
            Assert.Equal("SimpleOutput", output.ValueWhen);
        }

        [Theory]
        [InlineData("RunScript", "Output", "script")]
        [InlineData("ForEach", "ItemName", "item")]
        [InlineData("GetPropertyValue", "Output", "value")]
        [InlineData("SendRequest", "Output", "object")]
        public void OutputKinds(string step, string arg, string kind)
        {
            Assert.Equal(kind, Arg(step, arg).Arg.Output.Kind);
        }

        // The step picker finds a step by what you want to do, e.g. "monsters" finds Find.
        [Theory]
        [InlineData("Find", "tokens")]
        [InlineData("Find", "monsters")]
        [InlineData("RollDice", "random")]
        [InlineData("GetPropertyValue", "hp")]
        [InlineData("RequestUserInput", "ask")]
        public void Keywords_SayWhatAStepIsFor(string step, string keyword)
        {
            Assert.Contains(keyword, StepDefinitionMetadata.Build(AllSteps().Single(s => s.Value == step)).Keywords);
        }

        [Fact]
        public void EverySummaryPlaceholder_IsAnArgumentOfThatStep()
        {
            var problems = AllSteps()
                .Where(s => s.Summary != null)
                .SelectMany(s => Regex.Matches(s.Summary, @"\{(\w+)\}").Select(m => m.Groups[1].Value)
                    .Where(name => s.DataType?.GetProperty(name, BindingFlags.Public | BindingFlags.Instance) == null)
                    .Select(name => $"{s.Value}: {{{name}}}"))
                .ToList();

            Assert.Empty(problems);
        }

        // The editor shows these as help text; a field without one leaves addon authors guessing
        // (e.g. Add Menu Item's Location gave no hint which menu ids exist).
        [Fact]
        public void EveryStepAndArgument_HasADescription()
        {
            var problems = AllSteps()
                .Select(StepDefinitionMetadata.Build)
                .SelectMany(d => (string.IsNullOrWhiteSpace(d.Description) ? new[] { $"{d.Value}: step description" } : Array.Empty<string>())
                    .Concat(d.Arguments.Where(a => string.IsNullOrWhiteSpace(a.Description)).Select(a => $"{d.Value}.{a.Name}")))
                .ToList();

            Assert.Empty(problems);
        }

        [Fact]
        public void AddMenuItem_Location_IsAMenuLocationPicker()
        {
            Assert.Equal("menulocation", Arg("AddMenuItem", "Location").Arg.Type);
        }

        [Fact]
        public void EveryStep_HasASummary()
        {
            Assert.Empty(AllSteps().Where(s => string.IsNullOrWhiteSpace(s.Summary)).Select(s => s.Value));
        }

        [Fact]
        public void RunScript_ScriptIsCodeAndDeferred_OutputIsOutput()
        {
            var script = Arg("RunScript", "Script");
            Assert.Equal("code", script.Arg.Type);
            Assert.True(script.Arg.Deferred);
            Assert.False(script.Arg.IsOutput);
            Assert.Equal("Script {Script}[ → {Output}]", script.Step.Summary);

            Assert.True(Arg("RunScript", "Output").Arg.IsOutput);
        }

        [Theory]
        [InlineData("If", "Then")]
        [InlineData("If", "Else")]
        [InlineData("ForEach", "Do")]
        public void Blocks_UseTheStepsWidget(string step, string arg)
        {
            Assert.Equal("steps", Arg(step, arg).Arg.Type);
        }

        [Theory]
        [InlineData("ExecuteAction", "Value")]
        [InlineData("RollDice", "FollowUpActionName")]
        public void ActionReferences_UseActionWidget(string step, string arg)
        {
            Assert.Equal("action", Arg(step, arg).Arg.Type);
        }

        [Theory]
        [InlineData("GetConnectedPlayers", "Value")]
        [InlineData("SetVariable", "Name")]
        [InlineData("ForEach", "ItemName")]
        [InlineData("GetPropertyValue", "Output")]
        [InlineData("RollDice", "OutputVariable")]
        public void VariableCreatingArguments_AreMarkedAsOutputs(string step, string arg)
        {
            Assert.True(Arg(step, arg).Arg.IsOutput);
        }

        [Fact]
        public void Blocks_AreDeferred_ButOrdinaryArgumentsAreNot()
        {
            Assert.True(Arg("If", "Then").Arg.Deferred);
            Assert.True(Arg("ForEach", "Do").Arg.Deferred);
            Assert.False(Arg("If", "Condition").Arg.Deferred);
        }

        [Fact]
        public void EveryHook_ListsItsVariables()
        {
            var hooks = HookInfo.GetAll();
            Assert.NotEmpty(hooks);
            Assert.Empty(hooks.Where(h => h.Variables.Length == 0).Select(h => h.Key));
            Assert.Equal(new[] { "ChatCommand", "ChatArgs", "ChatText", "Player" },
                hooks.Single(h => h.Key == nameof(Hook.ChatCommand)).Variables);
        }

        // The editor offers the kinds Find knows instead of a free-text What.
        [Fact]
        public void Find_What_IsAKindPicker()
        {
            Assert.Equal("findkind", Arg("Find", "What").Arg.Type);
        }

        [Theory]
        [InlineData("GetData")]
        [InlineData("GetDetail")]
        [InlineData("SetDetail")]
        [InlineData("Calculate")]
        [InlineData("FilterCollection")]
        [InlineData("QueryData")]
        [InlineData("QueryProperties")]
        [InlineData("SendCommand")]
        public void RemovedSteps_AreGone(string step)
        {
            Assert.DoesNotContain(AllSteps(), s => s.Value == step);
        }
    }
}
