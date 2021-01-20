namespace Mages.Core.Ast.Expressions
{
    /// <summary>
    /// Represents a function call.
    /// </summary>
    public sealed class CallExpression : AssignableExpression, IExpression
    {
        #region Fields

        private readonly IExpression _function;
        private readonly ArgumentsExpression _arguments;

        #endregion

        #region ctor

        /// <summary>
        /// Creates a new function call expression.
        /// </summary>
        public CallExpression(IExpression function, ArgumentsExpression arguments)
            : base(function.Start, arguments.End)
        {
            _function = function;
            _arguments = arguments;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets the associated function.
        /// </summary>
        public IExpression Function 
        {
            get { return _function; }
        }

        /// <summary>
        /// Gets the arguments to pass to the function.
        /// </summary>
        public ArgumentsExpression Arguments
        {
            get { return _arguments; }
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
