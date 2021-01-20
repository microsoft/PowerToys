namespace Mages.Core.Ast.Expressions
{
    /// <summary>
    /// Represents a delete expression.
    /// </summary>
    public sealed class DeleteExpression : ComputingExpression, IExpression
    {
        #region Fields

        private readonly IExpression _payload;

        #endregion

        #region ctor

        /// <summary>
        /// Creates a new delete statement with the given payload.
        /// </summary>
        /// <param name="start">The start position.</param>
        /// <param name="payload">The payload to transport.</param>
        public DeleteExpression(TextPosition start, IExpression payload)
            : base(start, payload.End)
        {
            _payload = payload;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets the stored payload.
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
            var expression = _payload;
            var member = _payload as MemberExpression;
            var isIdentifier = _payload is VariableExpression;

            if (member != null)
            {
                expression = member.Member;
                isIdentifier = expression is IdentifierExpression;
            }

            if (!isIdentifier)
            {
                var error = new ParseError(ErrorCode.IdentifierExpected, expression);
                context.Report(error);
            }
        }

        #endregion
    }
}
