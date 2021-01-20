namespace Mages.Core.Ast.Statements
{
    /// <summary>
    /// Represents the shared core of all statements.
    /// </summary>
    public abstract class BaseStatement
    {
        #region Fields

        private readonly TextPosition _start;
        private readonly TextPosition _end;

        #endregion

        #region ctor

        /// <summary>
        /// Creates a new statement.
        /// </summary>
        public BaseStatement(TextPosition start, TextPosition end)
        {
            _start = start;
            _end = end;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets the start position of the statement.
        /// </summary>
        public TextPosition Start
        {
            get { return _start; }
        }

        /// <summary>
        /// Gets the end position of the statement.
        /// </summary>
        public TextPosition End
        {
            get { return _end; }
        }

        #endregion
    }
}
