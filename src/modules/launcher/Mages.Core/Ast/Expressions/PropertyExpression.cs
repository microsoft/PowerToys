namespace Mages.Core.Ast.Expressions
{
    /// <summary>
    /// Represents a property (name-value pair) of an object.
    /// </summary>
    public sealed class PropertyExpression : ComputingExpression, IExpression
    {
        #region Fields

        private readonly IExpression _name;
        private readonly IExpression _value;

        #endregion

        #region ctor

        /// <summary>
        /// Creates a new property.
        /// </summary>
        public PropertyExpression(IExpression name, IExpression value)
            : base(name.Start, value.End)
        {
            _name = name;
            _value = value;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets the name of the property.
        /// </summary>
        public IExpression Name
        {
            get { return _name; }
        }

        /// <summary>
        /// Gets the value of the property.
        /// </summary>
        public IExpression Value
        {
            get { return _value; }
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
