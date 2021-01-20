namespace Mages.Core.Ast.Expressions
{
    using System;

    /// <summary>
    /// Represents an assignment expression.
    /// </summary>
    public sealed class AssignmentExpression : ComputingExpression, IExpression
    {
        #region Fields

        private readonly IExpression _variable;
        private readonly IExpression _value;

        #endregion

        #region ctor

        /// <summary>
        /// Creates a new assignment expression.
        /// </summary>
        public AssignmentExpression(IExpression variable, IExpression value)
            : base(variable.Start, value.End)
        {
            _variable = variable;
            _value = value;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets the variable (value on the left side).
        /// </summary>
        public IExpression Variable 
        {
            get { return _variable; }
        }

        /// <summary>
        /// Gets the variable name, if any.
        /// </summary>
        public String VariableName 
        {
            get 
            { 
                var variable = Variable as VariableExpression;

                if (variable != null)
                {
                    return variable.Name;
                }

                return null;
            }
        }

        /// <summary>
        /// Gets the value on the right side.
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
            if (!Variable.IsAssignable)
            {
                var error = new ParseError(ErrorCode.AssignableExpected, Variable);
                context.Report(error);
            }

            if (Value is EmptyExpression)
            {
                var error = new ParseError(ErrorCode.AssignmentValueRequired, Value);
                context.Report(error);
            }
        }

        #endregion
    }
}
