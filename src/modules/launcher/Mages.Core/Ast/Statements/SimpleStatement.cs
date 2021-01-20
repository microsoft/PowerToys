namespace Mages.Core.Ast.Statements
{
    /// <summary>
    /// Represents a simple statement containing an expression.
    /// </summary>
    public sealed class SimpleStatement : BaseStatement, IStatement
    {
        #region Fields

        private readonly IExpression _expression;

        #endregion

        #region ctor

        /// <summary>
        /// Creates a new simple statement.
        /// </summary>
        public SimpleStatement(IExpression expression, TextPosition end)
            : base(expression.Start, end)
        {
            _expression = expression;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets the contained expression.
        /// </summary>
        public IExpression Expression
        {
            get { return _expression; }
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

        /// <summary>
        /// Accepts the visitor by showing him around.
        /// </summary>
        /// <param name="visitor">The visitor walking the tree.</param>
        public void Accept(ITreeWalker visitor)
        {
            visitor.Visit(this);
        }

        #endregion
    }
}
