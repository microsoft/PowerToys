namespace Mages.Core.Ast.Statements
{
    using System;

    /// <summary>
    /// Represents a return statement.
    /// </summary>
    public sealed class ReturnStatement : BaseStatement, IStatement
    {
        #region Fields

        private readonly IExpression _expression;

        #endregion

        #region ctor

        /// <summary>
        /// Creates a new return statement with the given payload.
        /// </summary>
        /// <param name="expression">The payload to transport.</param>
        /// <param name="start">The start position.</param>
        /// <param name="end">The end position.</param>
        public ReturnStatement(IExpression expression, TextPosition start, TextPosition end)
            : base(start, end)
        {
            _expression = expression;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets the stored payload.
        /// </summary>
        public IExpression Expression
        {
            get { return _expression; }
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
