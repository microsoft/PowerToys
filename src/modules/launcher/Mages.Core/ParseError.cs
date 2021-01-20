namespace Mages.Core
{
    /// <summary>
    /// A class to encapsulate data of a parse error.
    /// </summary>
    public sealed class ParseError : ITextRange
    {
        #region Fields

        private readonly ErrorCode _code;
        private readonly ITextRange _range;

        #endregion

        #region ctor

        /// <summary>
        /// Creates a new parse error object with these properties.
        /// </summary>
        /// <param name="code">The code of the error.</param>
        /// <param name="range">The text range of the error.</param>
        public ParseError(ErrorCode code, ITextRange range)
        {
            _code = code;
            _range = range;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets the start position of the error.
        /// </summary>
        public TextPosition Start
        {
            get { return _range.Start; }
        }

        /// <summary>
        /// Gets the end position of the error.
        /// </summary>
        public TextPosition End
        {
            get { return _range.End; }
        }

        /// <summary>
        /// Gets the code of the error.
        /// </summary>
        public ErrorCode Code
        {
            get { return _code; }
        }

        #endregion
    }
}
