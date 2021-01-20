namespace Mages.Core.Ast.Statements
{
    /// <summary>
    /// Represents an if statement.
    /// </summary>
    public sealed class IfStatement : BaseStatement, IStatement
    {
        #region Fields

        private readonly IExpression _condition;
        private readonly IStatement _primary;
        private readonly IStatement _secondary;

        #endregion

        #region ctor

        /// <summary>
        /// Creates a new if statement.
        /// </summary>
        public IfStatement(IExpression condition, IStatement primary, IStatement secondary, TextPosition start)
            : base(start, secondary.End)
        {
            _condition = condition;
            _primary = primary;
            _secondary = secondary;
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

        /// <summary>
        /// Gets the primary statement.
        /// </summary>
        public IStatement Primary
        {
            get { return _primary; }
        }

        /// <summary>
        /// Gets the secondary statement.
        /// </summary>
        public IStatement Secondary
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
