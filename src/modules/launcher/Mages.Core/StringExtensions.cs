namespace Mages.Core
{
    using Mages.Core.Source;
    using Mages.Core.Tokens;
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// A number of useful string extensions.
    /// </summary>
    public static class StringExtensions
    {
        /// <summary>
        /// Transforms the string to a token iterator.
        /// </summary>
        /// <param name="source">The string.</param>
        /// <returns>The created token iterator.</returns>
        public static IEnumerator<IToken> ToTokenStream(this String source)
        {
            var scanner = source.GetScanner();
            return scanner.ToTokenStream();
        }

        /// <summary>
        /// Gets a scanner to walk through the provided source.
        /// </summary>
        /// <param name="source">The string.</param>
        /// <returns>The created scanner.</returns>
        public static IScanner GetScanner(this String source)
        {
            return new StringScanner(source);
        }

        /// <summary>
        /// Checks if the source could be considered completed. This is not to
        /// indicate that the source is error free, but at least all open brackets
        /// have their partner.
        /// </summary>
        /// <param name="source">The string.</param>
        /// <returns>True if the source is completed, otherwise false.</returns>
        public static Boolean IsCompleted(this String source)
        {
            var tokens = source.ToTokenStream();
            var round = 0;
            var curly = 0;
            var square = 0;

            while (tokens.MoveNext())
            {
                switch (tokens.Current.Type)
                {
                    case TokenType.OpenScope: curly = curly + 1; break;
                    case TokenType.CloseScope: curly = Math.Max(curly - 1, 0); break;
                    case TokenType.OpenGroup: round = round + 1; break;
                    case TokenType.CloseGroup: round = Math.Max(round - 1, 0); break;
                    case TokenType.OpenList: square = square + 1; break;
                    case TokenType.CloseList: square = Math.Max(square - 1, 0); break;
                }
            }

            return round == 0 && curly == 0 && square == 0;
        }
    }
}
