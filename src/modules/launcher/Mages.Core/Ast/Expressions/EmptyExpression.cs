namespace Mages.Core.Ast.Expressions
{
    /// <summary>
    /// Represents an empty expression (potentially invalid).
    /// </summary>
    public sealed class EmptyExpression : ComputingExpression, IExpression
    {
        #region ctor

        /// <summary>
        /// Creates a new empty expression.
        /// </summary>
        public EmptyExpression(TextPosition position)
            : base(position, position)
        {
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
