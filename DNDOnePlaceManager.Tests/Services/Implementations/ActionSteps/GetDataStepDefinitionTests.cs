using DndOnePlaceManager.Application.Exceptions;
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
    // A Get Data step without an Output failed with .NET's "Value cannot be null.
    // (Parameter 'key')" when it stored the result under a null variable name.
    public class GetDataStepDefinitionTests
    {
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public async Task Execute_NoOutput_ExplainsThatOutputIsRequired(string? output)
        {
            var step = new ActionStep { Type = "GetData", Data = JObject.FromObject(new { Type = "Map", Name = "Default", Output = output }) };

            var ex = await Assert.ThrowsAsync<ActionProcessException>(() =>
                new GetDataStepDefinition().Execute(Mock.Of<IMediator>(), new Dictionary<string, object>(), null, step));

            Assert.Contains("'Output' is required", ex.Message);
        }
    }
}
