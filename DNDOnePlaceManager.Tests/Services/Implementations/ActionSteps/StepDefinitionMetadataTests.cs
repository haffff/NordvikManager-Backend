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
        [InlineData("If", "ActionTrue")]
        [InlineData("If", "ActionFalse")]
        [InlineData("ForEach", "Action")]
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
        [InlineData("FilterCollection", "ItemName")]
        [InlineData("GetPropertyValue", "Output")]
        [InlineData("RollDice", "OutputVariable")]
        public void VariableCreatingArguments_AreMarkedAsOutputs(string step, string arg)
        {
            Assert.True(Arg(step, arg).Arg.IsOutput);
        }

        [Fact]
        public void FilterCondition_IsDeferred_ButOrdinaryArgumentsAreNot()
        {
            Assert.True(Arg("FilterCollection", "Condition").Arg.Deferred);
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
    }
}
