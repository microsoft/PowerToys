namespace Mages.Core.Tokens
{
    using Mages.Core.Source;
    using System;

    sealed class PreprocessorToken : IToken
    {
        private readonly TextPosition _start;
        private readonly TextPosition _end;
        private readonly String _payload;

        public PreprocessorToken(String payload, TextPosition start, TextPosition end)
        {
            _payload = payload;
            _start = start;
            _end = end;
        }

        public TokenType Type
        {
            get { return TokenType.Preprocessor; }
        }

        public TextPosition End
        {
            get { return _end; }
        }

        public TextPosition Start
        {
            get { return _start; }
        }

        public String Command
        {
            get
            {
                if (_payload.Length > 0 && Specification.IsNameStart((Int32)_payload[0]))
                {
                    var length = 1;

                    while (length < _payload.Length && Specification.IsName((Int32)_payload[length]))
                    {
                        length++;
                    }

                    return _payload.Substring(0, length);
                }

                return String.Empty;
            }
        }

        public String Payload
        {
            get { return _payload; }
        }
    }
}