namespace Mages.Core.Tokens
{
    using System;

    sealed class CharacterToken : IToken
    {
        private readonly TokenType _type;
        private readonly Int32 _character;
        private readonly TextPosition _position;

        public CharacterToken(TokenType type, Int32 character, TextPosition position)
        {
            _type = type;
            _character = character;
            _position = position;
        }

        public TokenType Type
        {
            get { return _type; }
        }

        public String Payload
        {
            get { return Char.ConvertFromUtf32(_character); }
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
