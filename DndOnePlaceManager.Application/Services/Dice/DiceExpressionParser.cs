using System;
using System.Collections.Generic;
using DndOnePlaceManager.Application.Exceptions;

namespace DndOnePlaceManager.Application.Services.Dice
{
    // Recursive-descent parser over DiceExpressionLexer's token stream.
    //
    // Grammar:
    //   expr     := term (('+' | '-') term)*
    //   term     := factor (('*' | '/') factor)*
    //   factor   := '-' factor | '(' expr ')' | diceTerm | number
    //   diceTerm := [count] ('d' sides | 'dF') modifier*
    //   modifier := keepDrop | '!' | successFail
    //   keepDrop := ('kh'|'kl'|'dh'|'dl') [count]
    //   successFail := ('cs'|'cf') ('>'|'<'|'=') number
    internal sealed class DiceExpressionParser
    {
        private readonly List<DiceToken> _tokens;
        private int _pos;

        private DiceExpressionParser(List<DiceToken> tokens)
        {
            _tokens = tokens;
        }

        public static DiceExpressionNode Parse(string expression)
        {
            var tokens = DiceExpressionLexer.Tokenize(expression);
            var parser = new DiceExpressionParser(tokens);
            var node = parser.ParseExpr();
            parser.Expect(DiceTokenKind.EndOfInput);
            return node;
        }

        private DiceToken Current => _tokens[_pos];

        private DiceToken Advance()
        {
            var token = Current;
            if (_pos < _tokens.Count - 1) _pos++;
            return token;
        }

        private bool Check(DiceTokenKind kind) => Current.Kind == kind;

        private DiceToken Expect(DiceTokenKind kind)
        {
            if (!Check(kind)) throw new WrongArgumentsException("Roll");
            return Advance();
        }

        private static bool IsDiceStart(string text) =>
            text.Equals("d", StringComparison.OrdinalIgnoreCase) || text.Equals("df", StringComparison.OrdinalIgnoreCase);

        private DiceExpressionNode ParseExpr()
        {
            var left = ParseTerm();
            while (Check(DiceTokenKind.Plus) || Check(DiceTokenKind.Minus))
            {
                var opToken = Advance();
                var right = ParseTerm();
                left = new BinaryOpNode(opToken.Kind == DiceTokenKind.Plus ? '+' : '-', left, right);
            }
            return left;
        }

        private DiceExpressionNode ParseTerm()
        {
            var left = ParseFactor();
            while (Check(DiceTokenKind.Star) || Check(DiceTokenKind.Slash))
            {
                var opToken = Advance();
                var right = ParseFactor();
                left = new BinaryOpNode(opToken.Kind == DiceTokenKind.Star ? '*' : '/', left, right);
            }
            return left;
        }

        private DiceExpressionNode ParseFactor()
        {
            if (Check(DiceTokenKind.Minus))
            {
                Advance();
                return new BinaryOpNode('-', new NumberLiteralNode(0), ParseFactor());
            }

            if (Check(DiceTokenKind.LParen))
            {
                Advance();
                var inner = ParseExpr();
                Expect(DiceTokenKind.RParen);
                return inner;
            }

            if (Check(DiceTokenKind.Identifier) && IsDiceStart(Current.Text))
            {
                return ParseDiceTerm(1); // no explicit count, e.g. "d20"
            }

            if (Check(DiceTokenKind.Number))
            {
                var numberToken = Advance();
                if (Check(DiceTokenKind.Identifier) && IsDiceStart(Current.Text))
                {
                    return ParseDiceTerm((int)numberToken.Number);
                }
                return new NumberLiteralNode(numberToken.Number);
            }

            throw new WrongArgumentsException("Roll");
        }

        private DiceExpressionNode ParseDiceTerm(int count)
        {
            var dToken = Expect(DiceTokenKind.Identifier);
            bool isFudge = dToken.Text.Equals("df", StringComparison.OrdinalIgnoreCase);
            int sides = 0;

            if (!isFudge)
            {
                sides = (int)Expect(DiceTokenKind.Number).Number;
            }

            bool exploding = false;
            KeepDropModifier? keepDrop = null;
            SuccessFailModifier? successFail = null;

            while (true)
            {
                if (Check(DiceTokenKind.Bang))
                {
                    Advance();
                    exploding = true;
                    continue;
                }

                if (Check(DiceTokenKind.Identifier))
                {
                    var keyword = Current.Text.ToLowerInvariant();

                    if (keyword is "kh" or "kl" or "dh" or "dl")
                    {
                        Advance();
                        int keepDropCount = Check(DiceTokenKind.Number) ? (int)Advance().Number : 1;
                        var kind = keyword switch
                        {
                            "kh" => KeepDropKind.KeepHigh,
                            "kl" => KeepDropKind.KeepLow,
                            "dh" => KeepDropKind.DropHigh,
                            _ => KeepDropKind.DropLow,
                        };
                        keepDrop = new KeepDropModifier(kind, keepDropCount);
                        continue;
                    }

                    if (keyword is "cs" or "cf")
                    {
                        Advance();
                        bool isSuccess = keyword == "cs";
                        Comparator comparator = Check(DiceTokenKind.Greater) ? Comparator.GreaterThan
                            : Check(DiceTokenKind.Less) ? Comparator.LessThan
                            : Check(DiceTokenKind.Equal) ? Comparator.Equal
                            : throw new WrongArgumentsException("Roll");
                        Advance();
                        int threshold = (int)Expect(DiceTokenKind.Number).Number;
                        successFail = new SuccessFailModifier(isSuccess, comparator, threshold);
                        continue;
                    }
                }

                break;
            }

            return new DiceTermNode(count, sides, isFudge, exploding, keepDrop, successFail);
        }
    }
}
