using DndOnePlaceManager.Application.Exceptions;
using DNDOnePlaceManager.Services.Implementations.ActionBody;
using DNDOnePlaceManager.Services.Implementations.ActionSteps;
using MediatR;
using Moq;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Xunit;

namespace DNDOnePlaceManager.Tests.Services.Implementations.ActionSteps
{
    // Argument validation for DeleteCard / DeleteProperty / Delay. Their success paths go
    // through GameLobby.HandleCommand and aren't exercised here.
    public class UtilityStepDefinitionsTests
    {
        private readonly IMediator _mediator = Mock.Of<IMediator>();

        private Task Run(IActionStepDefinition def, object data) =>
            def.Execute(_mediator, new Dictionary<string, object>(), null, new ActionStep { Type = def.Value, Data = JObject.FromObject(data) });

        [Theory]
        [InlineData("-5")]
        [InlineData("1.5")]
        [InlineData("soon")]
        public async Task Delay_InvalidMilliseconds_Throws(string ms)
        {
            await Assert.ThrowsAsync<ActionProcessException>(() => Run(new DelayStepDefinition(), new { Milliseconds = ms }));
        }

        [Fact]
        public async Task Delay_Waits()
        {
            var sw = Stopwatch.StartNew();
            await Run(new DelayStepDefinition(), new { Milliseconds = "50" });
            Assert.True(sw.ElapsedMilliseconds >= 40);
        }

        [Fact]
        public async Task DeleteCard_InvalidId_Throws()
        {
            await Assert.ThrowsAsync<ActionProcessException>(() => Run(new DeleteCardStepDefinition(), new { CardId = "x" }));
        }

        [Fact]
        public async Task DeleteProperty_MissingName_Throws()
        {
            await Assert.ThrowsAsync<ActionProcessException>(() => Run(new DeletePropertyStepDefinition(),
                new { ParentId = "3f2504e0-4f89-11d3-9a0c-0305e82c3301", PropertyName = "" }));
        }
    }
}
