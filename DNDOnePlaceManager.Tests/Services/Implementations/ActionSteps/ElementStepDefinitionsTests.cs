using DndOnePlaceManager.Application.Exceptions;
using DNDOnePlaceManager.Services.Implementations.ActionBody;
using DNDOnePlaceManager.Services.Implementations.ActionSteps;
using MediatR;
using Moq;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace DNDOnePlaceManager.Tests.Services.Implementations.ActionSteps
{
    // Argument validation for the server-side map steps. The success path runs through
    // GameLobby.HandleCommand (real handlers + DB), which isn't exercised here.
    public class ElementStepDefinitionsTests
    {
        private readonly IMediator _mediator = Mock.Of<IMediator>();

        private static ActionStep MakeStep(object data) => new ActionStep { Type = "x", Data = JObject.FromObject(data) };

        private Task Run(IActionStepDefinition def, object data) =>
            def.Execute(_mediator, new Dictionary<string, object>(), null, MakeStep(data));

        [Theory]
        [InlineData("", "1", "2", "'ElementId' is required")]
        [InlineData("not-a-guid", "1", "2", "not a valid GUID")]
        [InlineData("3f2504e0-4f89-11d3-9a0c-0305e82c3301", "abc", "2", "'X' value 'abc' is not a number")]
        [InlineData("3f2504e0-4f89-11d3-9a0c-0305e82c3301", "1", "1,5", "'Y' value '1,5' is not a number")]
        public async Task MoveElement_InvalidArguments_Throw(string id, string x, string y, string expected)
        {
            var ex = await Assert.ThrowsAsync<ActionProcessException>(() =>
                Run(new MoveElementStepDefinition(), new { ElementId = id, X = x, Y = y }));
            Assert.Contains(expected, ex.Message);
        }

        [Fact]
        public async Task DeleteElement_MissingId_Throws()
        {
            await Assert.ThrowsAsync<ActionProcessException>(() =>
                Run(new DeleteElementStepDefinition(), new { ElementId = "" }));
        }

        [Fact]
        public async Task ChangeMap_InvalidMapId_Throws()
        {
            var ex = await Assert.ThrowsAsync<ActionProcessException>(() =>
                Run(new ChangeMapStepDefinition(), new { MapId = "nope", BattleMapId = Guid.NewGuid().ToString() }));
            Assert.Contains("'MapId'", ex.Message);
        }
    }
}
