using System.Collections.Generic;
using System.Linq;
using DndOnePlaceManager.Application.Exceptions;
using DndOnePlaceManager.Application.Services.Dice;
using Xunit;

namespace DndOnePlaceManager.Application.UnitTests.Services.Dice
{
    // Deterministic fake — returns queued values in order regardless of `sides`,
    // so tests can script exact dice outcomes (including exploding chains).
    internal class ScriptedRandomSource : IDiceRandomSource
    {
        private readonly Queue<int> _values;
        public ScriptedRandomSource(params int[] values) => _values = new Queue<int>(values);

        public int Roll(int sides) =>
            _values.Count > 0 ? _values.Dequeue() : throw new System.InvalidOperationException("ScriptedRandomSource ran out of scripted values");
    }

    public class DiceEngineTests
    {
        private static DiceEngine EngineWith(params int[] scriptedRolls) => new(new ScriptedRandomSource(scriptedRolls));

        [Fact]
        public void Evaluate_StandardNdMWithArithmetic_SumsRollsAndAppliesModifier()
        {
            var engine = EngineWith(4, 5);

            var result = engine.Evaluate("2d6+3");

            Assert.Equal(12, result.Result);
            Assert.Equal("{0}+3", result.Rolled);
            Assert.Equal(2, result.Dices.Length);
            Assert.All(result.Dices, d => Assert.True(d.Kept));
            Assert.Null(result.SuccessCount);
            Assert.Null(result.FailureCount);
        }

        [Fact]
        public void Evaluate_KeepHighest_DropsLowestRollsFromTheSum()
        {
            var engine = EngineWith(6, 1, 4, 3);

            var result = engine.Evaluate("4d6kh3");

            Assert.Equal(13, result.Result); // 6 + 4 + 3, the 1 is dropped
            var byValue = result.Dices.ToDictionary(d => d.Result);
            Assert.False(byValue[1].Kept);
            Assert.True(byValue[6].Kept);
            Assert.True(byValue[4].Kept);
            Assert.True(byValue[3].Kept);
        }

        [Fact]
        public void Evaluate_KeepLowest_DropsHighestRollsFromTheSum()
        {
            var engine = EngineWith(6, 1, 4, 3);

            var result = engine.Evaluate("4d6kl2");

            Assert.Equal(4, result.Result); // 1 + 3, the two highest (6, 4) are dropped
        }

        [Fact]
        public void Evaluate_ExplodingDie_ChainsWhileMaxIsRolledAndStopsOnNonMax()
        {
            // 1d6!: first roll is max (6) -> explodes -> max again (6) -> explodes -> 3 (stop)
            var engine = EngineWith(6, 6, 3);

            var result = engine.Evaluate("1d6!");

            Assert.Equal(15, result.Result); // 6 + 6 + 3
            Assert.Equal(3, result.Dices.Length);
            Assert.False(result.Dices[0].Exploded);
            Assert.True(result.Dices[1].Exploded);
            Assert.True(result.Dices[2].Exploded);
        }

        [Fact]
        public void Evaluate_SuccessCounting_CountsRollsPassingThresholdAndIgnoresOthers()
        {
            var engine = EngineWith(6, 5, 3, 2, 4);

            var result = engine.Evaluate("5d6cs>4");

            Assert.Equal(2, result.SuccessCount); // 6 and 5 pass
            Assert.Equal(0, result.FailureCount);
            Assert.Equal(2, result.Result);
        }

        [Fact]
        public void Evaluate_FudgeDice_MapsRawRollsToMinusOneZeroPlusOneAndSums()
        {
            // Raw rolls 1..3 map to -1, 0, +1 respectively.
            var engine = EngineWith(1, 3, 2, 1);

            var result = engine.Evaluate("4dF+8");

            Assert.Equal(7, result.Result); // (-1 + 1 + 0 - 1) + 8
            Assert.Equal(new[] { -1, 1, 0, -1 }, result.Dices.Select(d => d.Result));
        }

        [Theory]
        [InlineData("abc")]
        [InlineData("2d")]
        [InlineData("2d6/0")]
        public void Evaluate_MalformedOrInvalidExpression_ThrowsWrongArgumentsException(string expression)
        {
            var engine = EngineWith(1, 1, 1, 1, 1, 1, 1, 1);

            Assert.Throws<WrongArgumentsException>(() => engine.Evaluate(expression));
        }
    }
}
