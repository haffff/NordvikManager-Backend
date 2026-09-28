using DndOnePlaceManager.Application.Exceptions;
using DNDOnePlaceManager.Services.Implementations.ActionBody;
using DNDOnePlaceManager.Services.Implementations.ActionSteps;
using Moq;
using MediatR;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace DNDOnePlaceManager.Tests.Services.Implementations.ActionSteps
{
    // Regression coverage for Tier 0 control-flow fixes: If.OutputName was documented but
    // never written, FilterCollection evaluated its (pre-substituted) condition once instead
    // of per item, and Exit had no step definition so it never showed up in the editor.
    public class ControlFlowStepDefinitionsTests
    {
        private readonly IMediator _mediator = Mock.Of<IMediator>();

        private static ActionStep MakeStep(string type, object data) =>
            new ActionStep { Type = type, Data = JObject.FromObject(data) };

        [Fact]
        public async Task If_OutputName_StoresResultWithoutBranchActions()
        {
            var vars = new Dictionary<string, object>();

            await new IfStepDefinition().Execute(_mediator, vars, null,
                MakeStep("If", new { Condition = "3 > 2", OutputName = "isBigger" }));

            Assert.Equal(true, vars["isBigger"]);
        }

        [Fact]
        public async Task If_NonBooleanCondition_Throws()
        {
            await Assert.ThrowsAsync<ActionProcessException>(() =>
                new IfStepDefinition().Execute(_mediator, new Dictionary<string, object>(), null,
                    MakeStep("If", new { Condition = "3 + 2" })));
        }

        [Fact]
        public void Filter_DeclaresConditionAsDeferred()
        {
            Assert.Contains("Condition", new FilterStepDefinition().DeferredArguments);
        }

        [Fact]
        public async Task Filter_EvaluatesConditionPerItem()
        {
            var vars = new Dictionary<string, object>
            {
                ["numbers"] = new List<object> { 1, 5, 10, 15 },
                ["threshold"] = 7,
            };

            await new FilterStepDefinition().Execute(_mediator, vars, null, MakeStep("FilterCollection", new
            {
                Collection = "numbers",
                ItemName = "n",
                Condition = "%n% > %threshold%",
                OutputName = "big",
            }));

            Assert.Equal(new object[] { 10, 15 }, ((IEnumerable<object>)vars["big"]).ToArray());
            Assert.False(vars.ContainsKey("n"));
        }

        [Fact]
        public async Task Filter_ObjectItems_UseVariablePaths_AndRestoreShadowedVariable()
        {
            var vars = new Dictionary<string, object>
            {
                ["cards"] = new List<object> { JObject.Parse("{\"name\":\"Orc\",\"hp\":0}"), JObject.Parse("{\"name\":\"Elf\",\"hp\":4}") },
                ["c"] = "outer",
            };

            await new FilterStepDefinition().Execute(_mediator, vars, null, MakeStep("FilterCollection", new
            {
                Collection = "cards",
                ItemName = "c",
                Condition = "%v:c.hp% > 0",
                OutputName = "alive",
            }));

            var alive = Assert.Single((IEnumerable<object>)vars["alive"]);
            Assert.Equal("Elf", ((JObject)alive)["name"].ToString());
            Assert.Equal("outer", vars["c"]);
        }

        [Fact]
        public async Task Exit_ThrowsExitWithMessage()
        {
            var ex = await Assert.ThrowsAsync<ActionExitException>(() =>
                new ExitStepDefinition().Execute(_mediator, new Dictionary<string, object>(), null,
                    MakeStep("Exit", new { Message = "No target selected" })));

            Assert.Equal("No target selected", ex.ExitMessage);
        }

        [Fact]
        public async Task Exit_WithoutMessage_HasNullExitMessage()
        {
            var ex = await Assert.ThrowsAsync<ActionExitException>(() =>
                new ExitStepDefinition().Execute(_mediator, new Dictionary<string, object>(), null,
                    new ActionStep { Type = "Exit", Data = new JObject() }));

            Assert.Null(ex.ExitMessage);
        }
    }
}
