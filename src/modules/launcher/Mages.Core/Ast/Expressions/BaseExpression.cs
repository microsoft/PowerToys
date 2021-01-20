namespace Mages.Core.Ast.Expressions
{
    /// <summary>
    /// Represents the shared core of all expressions.
    /// </summary>
    public abstract class BaseExpression
    {
        #region Fields

        private readonly TextPosition _start;
        private readonly TextPosition _end;

        #endregion

        #region ctor

        /// <summary>
        /// Creates a new expression.
        /// </summary>
        public BaseExpression(TextPosition start, TextPosition end)
        {
            _start = start;
            _end = end;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets the start position of the expression.
        /// </summary>
        public TextPosition Start
        {
            get { return _start; }
        }

        /// <summary>
        /// Gets the end position of the expression.
        /// </summary>
        public TextPosition End
        {
            get { return _end; }
        }

        #endregion
    }
}
