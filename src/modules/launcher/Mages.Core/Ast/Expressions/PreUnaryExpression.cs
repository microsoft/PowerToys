namespace Mages.Core.Ast.Expressions
{
    using System;

    /// <summary>
    /// Base class for all pre unary expressions.
    /// </summary>
    public abstract class PreUnaryExpression : ComputingExpression, IExpression
    {
        #region Fields

        private readonly IExpression _value;
        private readonly String _operator;

        #endregion

        #region ctor

        /// <summary>
        /// Creates a new pre unary expression.
        /// </summary>
        public PreUnaryExpression(TextPosition start, IExpression value, String op)
            : base(start, value.End)
        {
            _value = value;
            _operator = op;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets the used value.
        /// </summary>
        public IExpression Value
        {
            get { return _value; }
        }

        /// <summary>
        /// Gets the operator string.
        /// </summary>
        public String Operator
        {
            get { return _operator; }
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
        public virtual void Validate(IValidationContext context)
        {
            if (_value is EmptyExpression)
            {
                var error = new ParseError(ErrorCode.OperandRequired, _value);
                context.Report(error);
            }
        }

        #endregion

        #region Operations

        internal sealed class Not : PreUnaryExpression
        {
            public Not(TextPosition start, IExpression value)
                : base(start, value, "~")
            {
            }
        }

        internal sealed class Minus : PreUnaryExpression
        {
            public Minus(TextPosition start, IExpression value)
                : base(start, value, "-")
            {
            }
        }

        internal sealed class Plus : PreUnaryExpression
        {
            public Plus(TextPosition start, IExpression value)
                : base(start, value, "+")
            {
            }
        }

        internal sealed class Type : PreUnaryExpression
        {
            public Type(TextPosition start, IExpression value)
                : base(start, value, "&")
            {
            }
        }

        internal sealed class Increment : PreUnaryExpression
        {
            public Increment(TextPosition start, IExpression value)
                : base(start, value, "++")
            {
            }

            public override void Validate(IValidationContext context)
            {
                if (Value is AssignableExpression == false)
                {
                    var error = new ParseError(ErrorCode.IncrementOperand, Value);
                    context.Report(error);
                }
            }
        }

        internal sealed class Decrement : PreUnaryExpression
        {
            public Decrement(TextPosition start, IExpression value)
                : base(start, value, "--")
            {
            }

            public override void Validate(IValidationContext context)
            {
                if (Value is AssignableExpression == false)
                {
                    var error = new ParseError(ErrorCode.DecrementOperand, Value);
                    context.Report(error);
                }
            }
        }

        #endregion
    }
}
