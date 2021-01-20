namespace Mages.Core.Source
{
    using Mages.Core.Tokens;
    using System;
    using System.Collections.Generic;

    static class ScannerExtensions
    {
        private static readonly NumberTokenizer Number = new NumberTokenizer();
        private static readonly StringTokenizer String = new StringTokenizer();
        private static readonly CommentTokenizer Comment = new CommentTokenizer();
        private static readonly GeneralTokenizer Tokenizer = new GeneralTokenizer(Number, String, Comment);

        public static Boolean PeekMoveNext(this IScanner scanner, Int32 character)
        {
            if (scanner.MoveNext())
            {
                if (scanner.Current == character)
                {
                    return true;
                }

                scanner.MoveBack();
            }

            return false;
        }

        public static Int32 Peek(this IScanner scanner)
        {
            if (scanner.MoveNext())
            {
                var character = scanner.Current;
                scanner.MoveBack();
                return character;
            }

            return CharacterTable.End;
        }

        public static IEnumerator<IToken> ToTokenStream(this IScanner scanner)
        {
            var token = default(IToken);

            do
            {
                token = Tokenizer.Next(scanner);
                yield return token;
            }
            while (token.Type != TokenType.End);
        }
    }
}
