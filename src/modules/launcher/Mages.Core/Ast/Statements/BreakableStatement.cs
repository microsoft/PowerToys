namespace Mages.Core.Ast.Statements
{
    /// <summary>
    /// Represents a breakable statement.
    /// </summary>
    public abstract class BreakableStatement : BaseStatement
    {
        #region Fields

        private readonly IStatement _body;

        #endregion

        #region ctor

        /// <summary>
        /// Creates a new breakable statement.
        /// </summary>
        /// <param name="body">The body to use.</param>
        /// <param name="start">The start position.</param>
        /// <param name="end">The end position.</param>
        public BreakableStatement(IStatement body, TextPosition start, TextPosition end)
            : base(start, end)
        {
            _body = body;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets the stored body.
        /// </summary>
        public IStatement Body
        {
            get { return _body; }
        }

        #endregion

        #region Methods

        /// <summary>
        /// Validates the expression with the given context.
        /// </summary>
        /// <param name="context">The validator to report errors to.</param>
        public void Validate(IValidationContext context)
        {
        }

        #endregion
    }
}
