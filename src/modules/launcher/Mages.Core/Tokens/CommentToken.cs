namespace Mages.Core.Tokens
{
    using System;

    sealed class CommentToken : IToken
    {
        private readonly String _comment;
        private readonly TextPosition _start;
        private readonly TextPosition _end;

        public CommentToken(String comment, TextPosition start, TextPosition end)
        {
            _comment = comment;
            _start = start;
            _end = end;
        }

        public TokenType Type
        {
            get { return TokenType.Comment; }
        }

        public String Payload
        {
            get { return _comment; }
        }

        public TextPosition Start
        {
            get { return _start; }
        }

        public TextPosition End
        {
            get { return _end; }
        }
    }
}
