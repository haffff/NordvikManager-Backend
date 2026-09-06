using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DndOnePlaceManager.Application.Exceptions;
using DndOnePlaceManager.Application.Services.Implementations.ChatTemplates;

namespace DndOnePlaceManager.Application.Services.Dice
{
    // Walks a parsed dice-expression AST, rolling dice via the injected random source
    // and folding the tree into a RollDefinition. Produces the same {index}-placeholder
    // "Rolled" string format the previous regex-based implementation did (consumed as-is
    // by RollChatTemplate.js's tokenizer), while the numeric Result is computed by
    // evaluating the tree directly instead of DataTable.Compute.
    internal sealed class DiceExpressionEvaluator
    {
        private const int MaxDiceCount = 1000;
        private const int MaxExplosionsPerDie = 100;

        private readonly IDiceRandomSource _random;
        private readonly List<DiceDefinition> _dice = new();
        private int _nextIndex;
        private int _successCount;
        private int _failureCount;
        private bool _hasSuccessFail;

        public DiceExpressionEvaluator(IDiceRandomSource random)
        {
            _random = random;
        }

        public RollDefinition Evaluate(string expression)
        {
            try
            {
                var ast = DiceExpressionParser.Parse(expression);
                var (display, value) = Visit(ast);

                return new RollDefinition
                {
                    Rolled = display,
                    Result = (int)Math.Round(value, MidpointRounding.AwayFromZero),
                    Dices = _dice.ToArray(),
                    SuccessCount = _hasSuccessFail ? _successCount : (int?)null,
                    FailureCount = _hasSuccessFail ? _failureCount : (int?)null,
                };
            }
            catch (WrongArgumentsException)
            {
                throw;
            }
            catch (Exception)
            {
                throw new WrongArgumentsException("Roll");
            }
        }

        private (string display, double value) Visit(DiceExpressionNode node)
        {
            switch (node)
            {
                case NumberLiteralNode number:
                    return (FormatNumber(number.Value), number.Value);

                case BinaryOpNode binary:
                    var (leftDisplay, leftValue) = Visit(binary.Left);
                    var (rightDisplay, rightValue) = Visit(binary.Right);
                    double result = binary.Op switch
                    {
                        '+' => leftValue + rightValue,
                        '-' => leftValue - rightValue,
                        '*' => leftValue * rightValue,
                        '/' when rightValue == 0 => throw new WrongArgumentsException("Roll"),
                        '/' => leftValue / rightValue,
                        _ => throw new WrongArgumentsException("Roll"),
                    };
                    return ($"{leftDisplay}{binary.Op}{rightDisplay}", result);

                case DiceTermNode term:
                    return VisitDiceTerm(term);

                default:
                    throw new WrongArgumentsException("Roll");
            }
        }

        private (string display, double value) VisitDiceTerm(DiceTermNode term)
        {
            if (term.Count <= 0 || term.Count > MaxDiceCount) throw new WrongArgumentsException("Roll");
            int sides = term.IsFudge ? 3 : term.Sides;
            if (sides <= 0) throw new WrongArgumentsException("Roll");

            int index = _nextIndex++;

            var rolls = new List<(int value, bool exploded)>();
            for (int i = 0; i < term.Count; i++)
            {
                RollOneWithExplosions(sides, term.IsFudge, term.Exploding, rolls);
            }

            var kept = Enumerable.Repeat(true, rolls.Count).ToArray();
            if (term.KeepDrop != null)
            {
                ApplyKeepDrop(rolls, kept, term.KeepDrop);
            }

            bool[]? success = null;
            if (term.SuccessFail != null)
            {
                _hasSuccessFail = true;
                success = new bool[rolls.Count];
                for (int i = 0; i < rolls.Count; i++)
                {
                    if (!kept[i]) continue;

                    bool passes = term.SuccessFail.Comparator switch
                    {
                        Comparator.GreaterThan => rolls[i].value > term.SuccessFail.Threshold,
                        Comparator.LessThan => rolls[i].value < term.SuccessFail.Threshold,
                        _ => rolls[i].value == term.SuccessFail.Threshold,
                    };
                    success[i] = passes;
                    if (!passes) continue;

                    if (term.SuccessFail.IsSuccess) _successCount++;
                    else _failureCount++;
                }
            }

            double termValue = 0;
            for (int i = 0; i < rolls.Count; i++)
            {
                if (!kept[i]) continue;
                termValue += term.SuccessFail != null
                    ? (success![i] ? (term.SuccessFail.IsSuccess ? 1 : -1) : 0)
                    : rolls[i].value;
            }

            for (int i = 0; i < rolls.Count; i++)
            {
                _dice.Add(new DiceDefinition(term.IsFudge ? 0 : term.Sides, term.Count, rolls[i].value, index)
                {
                    Kept = kept[i],
                    Exploded = rolls[i].exploded,
                    Success = success?[i],
                });
            }

            return ($"{{{index}}}", termValue);
        }

        // Rolls one die and, if exploding, chains re-rolls while the maximum face keeps
        // coming up. Capped so a scripted/adversarial random source can't hang evaluation.
        private void RollOneWithExplosions(int sides, bool isFudge, bool exploding, List<(int value, bool exploded)> rolls)
        {
            int raw = _random.Roll(sides);
            rolls.Add((MapRoll(raw, isFudge), false));

            int chain = 0;
            while (exploding && raw == sides && chain < MaxExplosionsPerDie)
            {
                chain++;
                raw = _random.Roll(sides);
                rolls.Add((MapRoll(raw, isFudge), true));
            }
        }

        // Fudge dice are a 3-sided die whose faces are -1/0/+1 rather than 1/2/3.
        private static int MapRoll(int raw, bool isFudge) => isFudge ? raw - 2 : raw;

        // Keep/drop only ever applies to the initial (non-exploded) dice — an exploded
        // bonus die is always kept alongside whichever initial die triggered it.
        private static void ApplyKeepDrop(List<(int value, bool exploded)> rolls, bool[] kept, KeepDropModifier modifier)
        {
            var initialIndices = new List<int>();
            for (int i = 0; i < rolls.Count; i++)
            {
                if (!rolls[i].exploded) initialIndices.Add(i);
            }

            var orderedHighToLow = initialIndices.OrderByDescending(i => rolls[i].value).ToList();
            int count = Math.Min(modifier.Count, orderedHighToLow.Count);

            HashSet<int> selected = modifier.Kind switch
            {
                KeepDropKind.KeepHigh or KeepDropKind.DropHigh => orderedHighToLow.Take(count).ToHashSet(),
                _ => orderedHighToLow.Skip(Math.Max(0, orderedHighToLow.Count - count)).ToHashSet(),
            };

            bool selectedMeansKeep = modifier.Kind is KeepDropKind.KeepHigh or KeepDropKind.KeepLow;
            foreach (var i in initialIndices)
            {
                kept[i] = selectedMeansKeep ? selected.Contains(i) : !selected.Contains(i);
            }
        }

        private static string FormatNumber(double value) =>
            value == Math.Floor(value) ? ((long)value).ToString(CultureInfo.InvariantCulture) : value.ToString(CultureInfo.InvariantCulture);
    }
}
