namespace Mages.Core.Ast.Statements
{
    /// <summary>
    /// Represents an match statement.
    /// </summary>
    public sealed class MatchStatement : BaseStatement, IStatement
    {
        #region Fields

        private readonly IExpression _reference;
        private readonly IStatement _cases;

        #endregion

        #region ctor

        /// <summary>
        /// Creates a new match statement.
        /// </summary>
        public MatchStatement(IExpression reference, IStatement cases, TextPosition start)
            : base(start, cases.End)
        {
            _reference = reference;
            _cases = cases;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets the stored reference.
        /// </summary>
        public IExpression Reference
        {
            get { return _reference; }
        }

        /// <summary>
        /// Gets the associated cases.
        /// </summary>
        public IStatement Cases
        {
            get { return _cases; }
        }

        #endregion

        #region Methods

        /// <summary>
        /// Accepts the visitor by showing him around.
        /// </summary>
        /// <param name="visitor">The visitor walking the tree.</param>
        public void Accept(ITreeWalker visitor)
        {
            visitor.Visit(this);
        }

        /// <summary>
        /// Validates the expression with the given context.
        /// </summary>
        /// <param name="context">The validator to report errors to.</param>
        public void Validate(IValidationContext context)
        {
            if (_reference.IsEmpty())
            {
                var error = new ParseError(ErrorCode.ExpressionExpected, _reference);
                context.Report(error);
            }

            if (_cases is BlockStatement == false)
            {
                var error = new ParseError(ErrorCode.CasesExpected, _cases);
                context.Report(error);
            }
        }

        #endregion
    }
}
