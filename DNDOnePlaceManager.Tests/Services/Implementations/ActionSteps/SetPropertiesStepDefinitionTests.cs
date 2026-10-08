using DNDOnePlaceManager.Services.Implementations.ActionSteps;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace DNDOnePlaceManager.Tests.Services.Implementations.ActionSteps
{
    // SetProperties batches SetProperty. The point that needs pinning is that the
    // 'name=value' lines are split BEFORE variable substitution (Properties is deferred), so
    // a substituted value containing line breaks or '=' can't corrupt the list.
    public class SetPropertiesStepDefinitionTests
    {
        [Fact]
        public void Properties_IsDeferredFromVariableSubstitution()
        {
            var step = new SetPropertiesStepDefinition(Mock.Of<IServiceScopeFactory>());

            Assert.Contains("Properties", step.DeferredArguments);
        }

        [Fact]
        public void ParseLines_SplitsOnFirstEquals_AndSkipsBlankOrMalformedLines()
        {
            var result = SetPropertiesStepDefinition.ParseLines(
                "item_name=%name%\r\n\n  item_formula = a=b \nno-equals-here\n=noName\nitem_description=%description%");

            Assert.Equal(new[]
            {
                ("item_name", "%name%"),
                ("item_formula", "a=b"),
                ("item_description", "%description%"),
            }, result);
        }

        [Fact]
        public void ParseLines_Empty_ReturnsNothing()
        {
            Assert.Empty(SetPropertiesStepDefinition.ParseLines("  "));
            Assert.Empty(SetPropertiesStepDefinition.ParseLines(null));
        }
    }
}
