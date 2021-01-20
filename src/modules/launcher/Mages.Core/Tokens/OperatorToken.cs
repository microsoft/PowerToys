namespace Mages.Core.Tokens
{
    using System;

    sealed class OperatorToken : IToken
    {
        private readonly TokenType _type;
        private readonly String _payload;
        private readonly TextPosition _start;
        private readonly TextPosition _end;

        public OperatorToken(TokenType type, String payload, TextPosition position)
            : this(type, payload, position, position)
        {
        }

        public OperatorToken(TokenType type, String payload, TextPosition start, TextPosition end)
        {
            _type = type;
            _payload = payload;
            _start = start;
            _end = end;
        }

        public TokenType Type
        {
            get { return _type; }
        }

        public String Payload
        {
            get { return _payload; }
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
