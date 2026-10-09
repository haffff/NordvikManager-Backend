using DndOnePlaceManager.Application.Services.Dice;
using Xunit;

namespace DndOnePlaceManager.Application.UnitTests.Services.Dice
{
    /// <summary>
    /// Formulas the frontend dice roller UI builds (NordvikManagerFrontEnd
    /// src/helpers/diceFormula.test.js). If the parser stops accepting one of these,
    /// the roller's "Roll" button would post "Invalid Roll" to chat.
    /// </summary>
    public class DiceRollerFormulaTests
    {
        [Theory]
        [InlineData("1d20")]
        [InlineData("3d6")]
        [InlineData("2d6+1d20+1d100")]
        [InlineData("1d20+5")]
        [InlineData("1d20-2")]
        [InlineData("4dF")]
        [InlineData("2d20kh1+5")]
        [InlineData("2d20kl1")]
        [InlineData("1d6+2d20kh1")]
        [InlineData("2d20!kh1")]
        [InlineData("4d6dl1")]
        [InlineData("4d6kh3")]
        [InlineData("3d6!")]
        [InlineData("1d6!+2dF")]
        [InlineData("10d10!cs>7")]
        [InlineData("5d10cf=1")]
        [InlineData("6d10!kh3cs>7-1")]
        public void Evaluate_AcceptsFormula_WhenBuiltByTheDiceRollerUi(string formula)
        {
            var engine = new DiceEngine(new DiceRandomSource());

            var roll = engine.Evaluate(formula);

            Assert.NotNull(roll);
            Assert.NotEmpty(roll.Dices);
        }
    }
}
