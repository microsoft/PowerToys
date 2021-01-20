namespace Mages.Core.Ast.Expressions
{
    /// <summary>
    /// Represents a function expression.
    /// </summary>
    public sealed class FunctionExpression : ComputingExpression, IExpression
    {
        #region Fields

        private readonly AbstractScope _scope;
        private readonly ParameterExpression _parameters;
        private readonly IStatement _body;

        #endregion

        #region ctor

        /// <summary>
        /// Creates a new function expression.
        /// </summary>
        public FunctionExpression(AbstractScope scope, ParameterExpression parameters, IStatement body)
            : base(parameters.Start, body.End)
        {
            _scope = scope;
            _parameters = parameters;
            _body = body;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets the associated abstract scope.
        /// </summary>
        public AbstractScope Scope
        {
            get { return _scope; }
        }

        /// <summary>
        /// Gets the defined parameters.
        /// </summary>
        public ParameterExpression Parameters
        {
            get { return _parameters; }
        }

        /// <summary>
        /// Gets the body to use.
        /// </summary>
        public IStatement Body
        {
            get { return _body; }
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
