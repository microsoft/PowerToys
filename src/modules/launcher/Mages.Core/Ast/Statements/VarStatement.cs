namespace Mages.Core.Ast.Statements
{
    using Mages.Core.Ast.Expressions;

    /// <summary>
    /// Represents a "var ...;" statement.
    /// </summary>
    public sealed class VarStatement : BaseStatement, IStatement
    {
        #region Fields

        private readonly IExpression _assignment;

        #endregion

        #region ctor

        /// <summary>
        /// Creates a new var statement.
        /// </summary>
        public VarStatement(IExpression assignment, TextPosition start, TextPosition end)
            : base(start, end)
        {
            _assignment = assignment;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets the associated assignment.
        /// </summary>
        public IExpression Assignment
        {
            get { return _assignment; }
        }

        #endregion

        #region Methods

        /// <summary>
        /// Validates the expression with the given context.
        /// </summary>
        /// <param name="context">The validator to report errors to.</param>
        public void Validate(IValidationContext context)
        {
            var assignment = _assignment as AssignmentExpression;

            if (assignment == null)
            {
                //TODO Report invalid construction
            }
            else if (assignment.VariableName == null)
            {
                //TODO Report invalid construction
            }
            else
            {
                //TODO Check against variable name (should be first / only 'var' with that name in the _current_ scope)
            }
        }

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
