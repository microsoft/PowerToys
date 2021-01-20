namespace Mages.Core.Ast.Expressions
{
    /// <summary>
    /// Represents a member expression.
    /// </summary>
    public sealed class MemberExpression : AssignableExpression, IExpression
    {
        #region Fields

        private readonly IExpression _obj;
        private readonly IExpression _member;

        #endregion

        #region ctor

        /// <summary>
        /// Creates a new member expression.
        /// </summary>
        public MemberExpression(IExpression obj, IExpression member)
            : base(obj.Start, member.End)
        {
            _obj = obj;
            _member = member;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets the associated object expression.
        /// </summary>
        public IExpression Object 
        {
            get { return _obj; }
        }

        /// <summary>
        /// Gets the associated member access.
        /// </summary>
        public IExpression Member
        {
            get { return _member; }
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
