namespace Mages.Core.Tokens
{
    using System;
    using System.Collections.Generic;

    static class TokenExtensions
    {
        public static IEnumerator<IToken> NextNonIgnorable(this IEnumerator<IToken> tokens)
        {
            while (tokens.MoveNext() && tokens.Current.IsIgnorable()) ;
            return tokens;
        }

        public static Boolean IsIgnorable(this IToken token)
        {
            return (int)token.Type > 65519;
        }

        public static Boolean IsNeither(this IToken token, TokenType a, TokenType b)
        {
            var type = token.Type;
            return type != a && type != b;
        }

        public static Boolean IsEither(this IToken token, TokenType a, TokenType b)
        {
            var type = token.Type;
            return type == a || type == b;
        }

        public static Boolean IsOneOf(this IToken token, TokenType a, TokenType b, TokenType c, TokenType d)
        {
            var type = token.Type;
            return type == a || type == b || type == c || type == d;
        }

        public static Boolean IsOneOf(this IToken token, TokenType a, TokenType b, TokenType c, TokenType d, TokenType e, TokenType f, TokenType g, TokenType h)
        {
            return token.IsOneOf(a, b, c, d) || token.IsOneOf(e, f, g, h);
        }

        public static Boolean Is(this IToken token, String keyword)
        {
            return token.Type == TokenType.Keyword && token.Payload.Equals(keyword, StringComparison.Ordinal);
        }
    }
}
