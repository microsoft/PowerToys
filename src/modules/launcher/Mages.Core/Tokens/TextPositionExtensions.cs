namespace Mages.Core.Tokens
{
    static class TextPositionExtensions
    {
        public static ITextRange ToRange(this TextPosition position)
        {
            var start = position;
            var end = new TextPosition(start.Row, start.Column + 1, start.Index + 1);
            return start.To(end);
        }

        public static ITextRange From(this TextPosition end, TextPosition start)
        {
            return start.To(end);
        }

        public static ITextRange To(this TextPosition start, TextPosition end)
        {
            return new TextRange(start, end);
        }

        struct TextRange : ITextRange
        {
            private readonly TextPosition _start;
            private readonly TextPosition _end;

            public TextRange(TextPosition start, TextPosition end)
            {
                _start = start;
                _end = end;
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
}
