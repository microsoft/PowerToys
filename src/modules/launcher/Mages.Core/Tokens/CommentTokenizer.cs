namespace Mages.Core.Tokens
{
    using Mages.Core.Source;
    using System;

    sealed class CommentTokenizer : ITokenizer
    {
        #region Methods

        public IToken Next(IScanner scanner)
        {
            var position = scanner.Position;

            if (scanner.MoveNext())
            {
                var current = scanner.Current;

                if (current == CharacterTable.Asterisk)
                {
                    return ScanBlock(scanner, position);
                }
                else if (current == CharacterTable.Slash)
                {
                    return ScanLine(scanner, position);
                }

                scanner.MoveBack();
            }
            
            return new OperatorToken(TokenType.RightDivide, "/", position);
        }

        #endregion

        #region Helpers

        private static IToken ScanLine(IScanner scanner, TextPosition start)
        {
            var sb = StringBuilderPool.Pull();

            while (scanner.MoveNext() && scanner.Current != CharacterTable.LineFeed)
            {
                sb.Append(Char.ConvertFromUtf32(scanner.Current));
            }

            return new CommentToken(sb.Stringify(), start, scanner.Position);
        }

        private static IToken ScanBlock(IScanner scanner, TextPosition start)
        {
            var sb = StringBuilderPool.Pull();

            while (scanner.MoveNext())
            {
                if (scanner.Current == CharacterTable.Asterisk)
                {
                    if (!scanner.MoveNext() || scanner.Current == CharacterTable.Slash)
                    {
                        break;
                    }
                    else
                    {
                        sb.Append(Char.ConvertFromUtf32(CharacterTable.Asterisk));
                    }
                }

                sb.Append(Char.ConvertFromUtf32(scanner.Current));
            }

            return new CommentToken(sb.Stringify(), start, scanner.Position);
        }

        #endregion
    }
}
