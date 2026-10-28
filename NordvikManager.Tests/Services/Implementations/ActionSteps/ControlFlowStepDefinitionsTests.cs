using NordvikManager.Application.Exceptions;
using NordvikManager.Services.Implementations.ActionBody;
using NordvikManager.Services.Implementations.ActionSteps;
using Moq;
using MediatR;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace NordvikManager.Tests.Services.Implementations.ActionSteps
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
