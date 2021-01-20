namespace Mages.Core.Tokens
{
    using System;

    sealed class EndToken : IToken
    {
        private readonly TextPosition _position;

        public EndToken(TextPosition position)
        {
            _position = position;
        }

        public TokenType Type
        {
            get { return TokenType.End; }
        }

        public String Payload
        {
            get { return String.Empty; }
        }

        public TextPosition Start
        {
            get { return _position; }
        }

        public TextPosition End
        {
            get { return _position; }
        }
    }
}
