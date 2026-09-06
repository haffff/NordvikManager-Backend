using System.Collections.Generic;

namespace DndOnePlaceManager.Application.Services.Dice
{
    internal abstract class DiceExpressionNode { }

    internal sealed class NumberLiteralNode : DiceExpressionNode
    {
        public double Value { get; }
        public NumberLiteralNode(double value) => Value = value;
    }

    internal sealed class BinaryOpNode : DiceExpressionNode
    {
        public char Op { get; }
        public DiceExpressionNode Left { get; }
        public DiceExpressionNode Right { get; }
        public BinaryOpNode(char op, DiceExpressionNode left, DiceExpressionNode right)
        {
            Op = op;
            Left = left;
            Right = right;
        }
    }

    internal enum KeepDropKind { KeepHigh, KeepLow, DropHigh, DropLow }

    internal sealed class KeepDropModifier
    {
        public KeepDropKind Kind { get; }
        public int Count { get; }
        public KeepDropModifier(KeepDropKind kind, int count)
        {
            Kind = kind;
            Count = count;
        }
    }

    internal enum Comparator { GreaterThan, LessThan, Equal }

    internal sealed class SuccessFailModifier
    {
        public bool IsSuccess { get; } // true = "cs" (count success), false = "cf" (count failure)
        public Comparator Comparator { get; }
        public int Threshold { get; }
        public SuccessFailModifier(bool isSuccess, Comparator comparator, int threshold)
        {
            IsSuccess = isSuccess;
            Comparator = comparator;
            Threshold = threshold;
        }
    }

    // A single "<count>d<sides>[modifiers]" term, e.g. "4d6kh3!cs>5". Sides is null
    // when IsFudge is true (dF is always a 3-sided -1/0/1 die).
    internal sealed class DiceTermNode : DiceExpressionNode
    {
        public int Count { get; }
        public int Sides { get; }
        public bool IsFudge { get; }
        public bool Exploding { get; }
        public KeepDropModifier? KeepDrop { get; }
        public SuccessFailModifier? SuccessFail { get; }

        public DiceTermNode(int count, int sides, bool isFudge, bool exploding,
            KeepDropModifier? keepDrop, SuccessFailModifier? successFail)
        {
            Count = count;
            Sides = sides;
            IsFudge = isFudge;
            Exploding = exploding;
            KeepDrop = keepDrop;
            SuccessFail = successFail;
        }
    }
}
