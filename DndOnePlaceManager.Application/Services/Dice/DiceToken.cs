namespace DndOnePlaceManager.Application.Services.Dice
{
    internal enum DiceTokenKind
    {
        Number,
        // Contiguous letter run, e.g. "d", "dF", "kh", "kl", "dh", "dl", "cs", "cf" —
        // the parser decides what a given identifier means based on context.
        Identifier,
        Plus,
        Minus,
        Star,
        Slash,
        Bang,
        Greater,
        Less,
        Equal,
        LParen,
        RParen,
        EndOfInput,
    }

    internal readonly struct DiceToken
    {
        public DiceTokenKind Kind { get; }
        public string Text { get; }
        public double Number { get; }
        public int Position { get; }

        public DiceToken(DiceTokenKind kind, string text, double number, int position)
        {
            Kind = kind;
            Text = text;
            Number = number;
            Position = position;
        }

        public override string ToString() => Kind == DiceTokenKind.Number ? Number.ToString() : Text;
    }
}
