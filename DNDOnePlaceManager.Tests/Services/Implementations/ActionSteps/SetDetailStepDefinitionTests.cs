using DNDOnePlaceManager.Services.Implementations.ActionBody;
using DNDOnePlaceManager.Services.Implementations.ActionSteps;
using MediatR;
using Moq;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace DNDOnePlaceManager.Tests.Services.Implementations.ActionSteps
{
    // Set Detail's Type help says 'string', 'int', 'bool' — it used Type.GetType(), which only
    // knows full .NET names, so "int" silently fell back to string.
    public class SetDetailStepDefinitionTests
    {
        private class Target
        {
            public int Hp { get; set; }
            public bool Alive { get; set; }
        }

        private static Task Run(Dictionary<string, object> variables, object data) =>
            new SetDetailStepDefinition().Execute(
                Mock.Of<IMediator>(), variables, null!, new ActionStep { Type = "SetDetail", Data = JObject.FromObject(data) });

        [Theory]
        [InlineData("int")]
        [InlineData("Int32")]
        public async Task Execute_ConvertsValue_WhenTypeIsAShortName(string type)
        {
            var target = new Target();
            var variables = new Dictionary<string, object> { ["hero"] = target };

            await Run(variables, new { Input = "hero", DetailName = "Hp", Value = "7", Type = type });

            Assert.Equal(7, target.Hp);
        }

        [Fact]
        public async Task Execute_ConvertsBool_WhenTypeIsBool()
        {
            var target = new Target();
            var variables = new Dictionary<string, object> { ["hero"] = target };

            await Run(variables, new { Input = "hero", DetailName = "Alive", Value = "true", Type = "bool" });

            Assert.True(target.Alive);
        }
    }
}
