namespace Mages.Core.Tokens
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    sealed class InterpolatedToken : IToken
    {
        private static readonly IEnumerable<ParseError> NoErrors = Enumerable.Empty<ParseError>();

        private readonly String _content;
        private readonly TextPosition _start;
        private readonly TextPosition _end;
        private readonly IEnumerable<ParseError> _errors;
        private readonly List<List<IToken>> _parts;

        public InterpolatedToken(String content, List<List<IToken>> parts, IEnumerable<ParseError> errors, TextPosition start, TextPosition end)
        {
            _content = content;
            _start = start;
            _end = end;
            _errors = errors ?? NoErrors;
            _parts = parts;
        }

        public Int32 ReplacementCount
        {
            get { return _parts.Count; }
        }

        public IEnumerable<IToken> this[Int32 index]
        {
            get { return _parts[index]; }
        }

        public IEnumerable<ParseError> Errors
        {
            get { return _errors; }
        }

        public TokenType Type
        {
            get { return TokenType.InterpolatedString; }
        }

        public String Payload
        {
            get { return _content; }
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
