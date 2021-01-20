namespace Mages.Core.Ast.Expressions
{
    /// <summary>
    /// Represents a conditional expression.
    /// </summary>
    public sealed class ConditionalExpression : ComputingExpression, IExpression
    {
        #region Fields

        private readonly IExpression _condition;
        private readonly IExpression _primary;
        private readonly IExpression _secondary;

        #endregion

        #region ctor

        /// <summary>
        /// Creates a new conditional expression.
        /// </summary>
        public ConditionalExpression(IExpression condition, IExpression primary, IExpression secondary)
            : base(condition.Start, secondary.End)
        {
            _condition = condition;
            _primary = primary;
            _secondary = secondary;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets the condition.
        /// </summary>
        public IExpression Condition 
        {
            get { return _condition; }
        }

        /// <summary>
        /// Gets the primary selected value.
        /// </summary>
        public IExpression Primary 
        {
            get { return _primary; }
        }

        /// <summary>
        /// Gets the alternative selected value.
        /// </summary>
        public IExpression Secondary
        {
            get { return _secondary; }
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
        }

        #endregion
    }
}
