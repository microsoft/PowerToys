namespace Mages.Core.Ast.Expressions
{
    /// <summary>
    /// Represents an invalid expression.
    /// </summary>
    public sealed class InvalidExpression : ComputingExpression, IExpression
    {
        #region Fields

        private readonly ErrorCode _error;
        private readonly ITextRange _payload;

        #endregion

        #region ctor

        /// <summary>
        /// Creates a new invalid expression.
        /// </summary>
        public InvalidExpression(ErrorCode error, ITextRange payload)
            : base(payload.Start, payload.End)
        {
            _error = error;
            _payload = payload;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets the payload covered by the container.
        /// </summary>
        public ITextRange Payload
        {
            get { return _payload; }
        }

        /// <summary>
        /// Gets the associated error code.
        /// </summary>
        public ErrorCode Error
        {
            get { return _error; }
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
            context.Report(new ParseError(_error, _payload));
        }

        #endregion
    }
}
