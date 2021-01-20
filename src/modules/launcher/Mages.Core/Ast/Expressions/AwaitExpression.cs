namespace Mages.Core.Ast.Expressions
{
    /// <summary>
    /// Represents a future wrapper.
    /// </summary>
    public sealed class AwaitExpression : ComputingExpression, IExpression
    {
        #region Fields

        private readonly IExpression _payload;

        #endregion

        #region ctor

        /// <summary>
        /// Creates a new await expression.
        /// </summary>
        public AwaitExpression(TextPosition start, IExpression payload)
            : base(start, payload.End)
        {
            _payload = payload;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets the carried payload to be awaited.
        /// </summary>
        public IExpression Payload
        {
            get { return _payload; }
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
