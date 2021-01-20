namespace Mages.Core.Tokens
{
    using Mages.Core.Source;
    using System;

    sealed class GeneralTokenizer : ITokenizer
    {
        #region Fields

        private readonly ITokenizer _number;
        private readonly ITokenizer _string;
        private readonly ITokenizer _comment;
        private readonly ITokenizer _interpolated;

        #endregion

        #region ctor

        public GeneralTokenizer(ITokenizer numberTokenizer, ITokenizer stringTokenizer, ITokenizer commentTokenizer)
        {
            _number = numberTokenizer;
            _string = stringTokenizer;
            _comment = commentTokenizer;
            _interpolated = new InterpolationTokenizer(this);
        }

        #endregion

        #region Methods

        public IToken Next(IScanner scanner)
        {
            if (scanner.MoveNext())
            {
                var current = scanner.Current;

                if (current.IsSpaceCharacter())
                {
                    return new CharacterToken(TokenType.Space, current, scanner.Position);
                }
                else if (current.IsNameStart())
                {
                    return ScanName(scanner);
                }
                else if (current.IsDigit())
                {
                    return _number.Next(scanner);
                }
                else
                {
                    return ScanSymbol(scanner);
                }
            }

            return new EndToken(scanner.Position);
        }

        #endregion

        #region States

        private IToken ScanSymbol(IScanner scanner)
        {
            var start = scanner.Position;

            switch (scanner.Current)
            {
                case CharacterTable.At:
                    var next = scanner.Peek();

                    if (next == CharacterTable.CurvedQuotationMark)
                    {
                        return _interpolated.Next(scanner);
                    }
                    else if (next == CharacterTable.DoubleQuotationMark)
                    {
                        return _string.Next(scanner);
                    }

                    return ScanName(scanner);
                case CharacterTable.FullStop:
                    return _number.Next(scanner);
                case CharacterTable.Comma:
                    return new CharacterToken(TokenType.Comma, CharacterTable.Comma, start);
                case CharacterTable.Colon:
                    return new CharacterToken(TokenType.Colon, CharacterTable.Colon, start);
                case CharacterTable.SemiColon:
                    return new CharacterToken(TokenType.SemiColon, CharacterTable.SemiColon, start);
                case CharacterTable.Plus:
                    if (scanner.PeekMoveNext(CharacterTable.Plus))
                    {
                        return new OperatorToken(TokenType.Increment, "++", start, scanner.Position);
                    }
                    
                    return new OperatorToken(TokenType.Add, "+", start);
                case CharacterTable.Minus:
                    if (scanner.PeekMoveNext(CharacterTable.Minus))
                    {
                        return new OperatorToken(TokenType.Decrement, "--", start, scanner.Position);
                    }

                    return new OperatorToken(TokenType.Subtract, "-", start);
                case CharacterTable.GreaterThan:
                    if (scanner.PeekMoveNext(CharacterTable.Equal))
                    {
                        return new OperatorToken(TokenType.GreaterEqual, ">=", start, scanner.Position);
                    }

                    return new OperatorToken(TokenType.Greater, ">", start);
                case CharacterTable.LessThan:
                    if (scanner.PeekMoveNext(CharacterTable.Equal))
                    {
                        return new OperatorToken(TokenType.LessEqual, "<=", start, scanner.Position);
                    }

                    return new OperatorToken(TokenType.Less, "<", start);
                case CharacterTable.CircumflexAccent:
                    return new OperatorToken(TokenType.Power, "^", start);
                case CharacterTable.Tilde:
                    if (scanner.PeekMoveNext(CharacterTable.Equal))
                    {
                        return new OperatorToken(TokenType.NotEqual, "~=", start, scanner.Position);
                    }

                    return new OperatorToken(TokenType.Negate, "~", start);
                case CharacterTable.ExclamationMark:
                    return new OperatorToken(TokenType.Factorial, "!", start);
                case CharacterTable.Equal:
                    return ScanEqual(scanner);
                case CharacterTable.Slash:
                    return _comment.Next(scanner);
                case CharacterTable.Backslash:
                    return new OperatorToken(TokenType.LeftDivide, "\\", start);
                case CharacterTable.Asterisk:
                    return new OperatorToken(TokenType.Multiply, "*", start);
                case CharacterTable.Percent:
                    return new OperatorToken(TokenType.Modulo, "%", start);
                case CharacterTable.Pipe:
                    if (scanner.PeekMoveNext(CharacterTable.Pipe))
                    {
                        return new OperatorToken(TokenType.Or, "||", start, scanner.Position);
                    }

                    return new OperatorToken(TokenType.Pipe, "|", start);
                case CharacterTable.Ampersand:
                    if (scanner.PeekMoveNext(CharacterTable.Ampersand))
                    {
                        return new OperatorToken(TokenType.And, "&&", start, scanner.Position);
                    }

                    return new OperatorToken(TokenType.Type, "&", start);
                case CharacterTable.QuestionMark:
                    return new OperatorToken(TokenType.Condition, "?", start);
                case CharacterTable.SingleQuotationMark:
                    return new OperatorToken(TokenType.Transpose, "'", start);
                case CharacterTable.DoubleQuotationMark:
                    return _string.Next(scanner);
                case CharacterTable.CurvedQuotationMark:
                    return _interpolated.Next(scanner);
                case CharacterTable.OpenBracket:
                    return new CharacterToken(TokenType.OpenGroup, CharacterTable.OpenBracket, start);
                case CharacterTable.CloseBracket:
                    return new CharacterToken(TokenType.CloseGroup, CharacterTable.CloseBracket, start);
                case CharacterTable.OpenArray:
                    return new CharacterToken(TokenType.OpenList, CharacterTable.OpenArray, start);
                case CharacterTable.CloseArray:
                    return new CharacterToken(TokenType.CloseList, CharacterTable.CloseArray, start);
                case CharacterTable.OpenScope:
                    return new CharacterToken(TokenType.OpenScope, CharacterTable.OpenScope, start);
                case CharacterTable.CloseScope:
                    return new CharacterToken(TokenType.CloseScope, CharacterTable.CloseScope, start);
                case CharacterTable.End:
                    return new EndToken(start);
                case CharacterTable.Hash:
                    return Preprocessor(scanner);
            }

            return new CharacterToken(TokenType.Unknown, scanner.Current, start);
        }

        private IToken Preprocessor(IScanner scanner)
        {
            var start = scanner.Position;
            var sb = StringBuilderPool.Pull();

            while (scanner.MoveNext())
            {
                var current = scanner.Current;

                if (current == CharacterTable.LineFeed)
                    break;

                sb.Append(Char.ConvertFromUtf32(current));
            }

            var end = scanner.Position;
            var payload = sb.Stringify();
            return new PreprocessorToken(payload, start, end);
        }

        private static IToken ScanName(IScanner scanner)
        {
            var position = scanner.Position;
            var sb = StringBuilderPool.Pull();
            var current = scanner.Current;
            var canContinue = true;

            do
            {
                sb.Append(Char.ConvertFromUtf32(current));
                
                if (!scanner.MoveNext())
                {
                    canContinue = false;
                    break;
                }

                current = scanner.Current;
            }
            while (current.IsName());

            if (canContinue)
            {
                scanner.MoveBack();
            }

            var name = sb.Stringify();
            var type = Keywords.IsKeyword(name) ? TokenType.Keyword : TokenType.Identifier;
            return new IdentToken(type, name, position, scanner.Position);
        }

        private static IToken ScanEqual(IScanner scanner)
        {
            var position = scanner.Position;

            if (scanner.MoveNext())
            {
                var current = scanner.Current;

                if (current == CharacterTable.Equal)
                {
                    return new OperatorToken(TokenType.Equal, "==", position, scanner.Position);
                }
                else if (current == CharacterTable.GreaterThan)
                {
                    return new OperatorToken(TokenType.Lambda, "=>", position, scanner.Position);
                }

                scanner.MoveBack();
            }

            return new OperatorToken(TokenType.Assignment, "=", position);
        }

        #endregion
    }
}