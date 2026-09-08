using System.Collections.Generic;
using System.Globalization;
using System.Text;
using DndOnePlaceManager.Application.Exceptions;

namespace DndOnePlaceManager.Application.Services.Dice
{
    // Splits a dice expression into tokens. Letters run together into one Identifier
    // token regardless of meaning (e.g. "kh", "dF", "cs") — the parser, not the lexer,
    // decides what a given identifier means from context. Digits run together into one
    // Number token. This keeps the lexer trivial and keyword-agnostic.
    internal static class DiceExpressionLexer
    {
        public static List<DiceToken> Tokenize(string expression)
        {
            var tokens = new List<DiceToken>();
            int i = 0;
            int len = expression.Length;

            while (i < len)
            {
                char c = expression[i];

                if (char.IsWhiteSpace(c))
                {
                    i++;
                    continue;
                }

                if (char.IsDigit(c) || (c == '.' && i + 1 < len && char.IsDigit(expression[i + 1])))
                {
                    int start = i;
                    bool sawDot = false;
                    while (i < len && (char.IsDigit(expression[i]) || (expression[i] == '.' && !sawDot)))
                    {
                        if (expression[i] == '.') sawDot = true;
                        i++;
                    }
                    var text = expression.Substring(start, i - start);
                    var value = double.Parse(text, CultureInfo.InvariantCulture);
                    tokens.Add(new DiceToken(DiceTokenKind.Number, text, value, start));
                    continue;
                }

                if (char.IsLetter(c))
                {
                    int start = i;
                    while (i < len && char.IsLetter(expression[i])) i++;
                    var text = expression.Substring(start, i - start);
                    tokens.Add(new DiceToken(DiceTokenKind.Identifier, text, 0, start));
                    continue;
                }

                switch (c)
                {
                    case '+': tokens.Add(new DiceToken(DiceTokenKind.Plus, "+", 0, i)); break;
                    case '-': tokens.Add(new DiceToken(DiceTokenKind.Minus, "-", 0, i)); break;
                    case '*': tokens.Add(new DiceToken(DiceTokenKind.Star, "*", 0, i)); break;
                    case '/': tokens.Add(new DiceToken(DiceTokenKind.Slash, "/", 0, i)); break;
                    case '!': tokens.Add(new DiceToken(DiceTokenKind.Bang, "!", 0, i)); break;
                    case '>': tokens.Add(new DiceToken(DiceTokenKind.Greater, ">", 0, i)); break;
                    case '<': tokens.Add(new DiceToken(DiceTokenKind.Less, "<", 0, i)); break;
                    case '=': tokens.Add(new DiceToken(DiceTokenKind.Equal, "=", 0, i)); break;
                    case '(': tokens.Add(new DiceToken(DiceTokenKind.LParen, "(", 0, i)); break;
                    case ')': tokens.Add(new DiceToken(DiceTokenKind.RParen, ")", 0, i)); break;
                    default:
                        throw new WrongArgumentsException("Roll");
                }
                i++;
            }

            tokens.Add(new DiceToken(DiceTokenKind.EndOfInput, string.Empty, 0, len));
            return tokens;
        }
    }
}
