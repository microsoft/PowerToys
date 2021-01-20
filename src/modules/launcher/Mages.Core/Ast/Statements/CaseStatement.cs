namespace Mages.Core.Ast.Statements
{
    /// <summary>
    /// Represents an case statement.
    /// </summary>
    public sealed class CaseStatement : BreakableStatement, IStatement
    {
        #region Fields

        private readonly IExpression _condition;

        #endregion

        #region ctor

        /// <summary>
        /// Creates a new case statement.
        /// </summary>
        public CaseStatement(IExpression condition, IStatement body)
            : base(body, condition.Start, body.End)
        {
            _condition = condition;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets the stored condition.
        /// </summary>
        public IExpression Condition
        {
            get { return _condition; }
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

        #endregion
    }
}
