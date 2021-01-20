namespace Mages.Core.Ast.Expressions
{
    using System;

    /// <summary>
    /// Represents the access of a variable.
    /// </summary>
    public sealed class VariableExpression : AssignableExpression, IExpression
    {
        #region Fields

        private readonly String _name;
        private readonly AbstractScope _scope;

        #endregion

        #region ctor

        /// <summary>
        /// Creates a new variable expression.
        /// </summary>
        public VariableExpression(String name, AbstractScope scope, TextPosition start, TextPosition end)
            : base(start, end)
        {
            _name = name;
            _scope = scope;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets the name of the variable.
        /// </summary>
        public String Name
        {
            get { return _name; }
        }

        /// <summary>
        /// Gets the assigned abstract scope.
        /// </summary>
        public AbstractScope Scope
        {
            get { return _scope; }
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
