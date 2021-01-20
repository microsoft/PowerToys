namespace Mages.Core.Ast.Statements
{
    using Mages.Core.Ast.Expressions;

    /// <summary>
    /// Represents a continue statement.
    /// </summary>
    public sealed class ContinueStatement : BaseStatement, IStatement
    {
        #region Fields

        private readonly IExpression _expression;

        #endregion

        #region ctor

        /// <summary>
        /// Creates a new continue statement with the given payload.
        /// </summary>
        /// <param name="expression">The payload.</param>
        /// <param name="start">The start position.</param>
        /// <param name="end">The end position.</param>
        public ContinueStatement(IExpression expression, TextPosition start, TextPosition end)
            : base(start, end)
        {
            _expression = expression;
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
            if (!context.IsInLoop)
            {
                var error = new ParseError(ErrorCode.LoopMissing, this);
                context.Report(error);
            }

            if (_expression is EmptyExpression == false)
            {
                var error = new ParseError(ErrorCode.TerminatorExpected, _expression);
                context.Report(error);
            }
        }

        #endregion
    }
}
