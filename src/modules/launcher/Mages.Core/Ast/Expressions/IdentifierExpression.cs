namespace Mages.Core.Ast.Expressions
{
    using System;

    /// <summary>
    /// Represents a generalized identifier, which is not a variable.
    /// </summary>
    public sealed class IdentifierExpression : ComputingExpression, IExpression
    {
        #region Fields

        private readonly String _name;

        #endregion

        #region ctor

        /// <summary>
        /// Creates a new identifier expression.
        /// </summary>
        public IdentifierExpression(String name, TextPosition start, TextPosition end)
            : base(start, end)
        {
            _name = name;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets the name of the identifier.
        /// </summary>
        public String Name
        {
            get { return _name; }
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
