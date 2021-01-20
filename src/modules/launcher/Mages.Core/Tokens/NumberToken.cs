namespace Mages.Core.Tokens
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;

    sealed class NumberToken : IToken
    {
        private static readonly IEnumerable<ParseError> NoErrors = Enumerable.Empty<ParseError>();

        private readonly Double _value;
        private readonly TextPosition _start;
        private readonly TextPosition _end;
        private readonly IEnumerable<ParseError> _errors;

        public NumberToken(Double value, IEnumerable<ParseError> errors, TextPosition start, TextPosition end)
        {
            _value = value;
            _start = start;
            _end = end;
            _errors = errors ?? NoErrors;
        }

        public IEnumerable<ParseError> Errors
        {
            get { return _errors; }
        }

        public Double Value 
        {
            get { return _value; }
        }

        public TokenType Type
        {
            get { return TokenType.Number; }
        }

        public String Payload
        {
            get { return _value.ToString(NumberFormatInfo.InvariantInfo); }
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
