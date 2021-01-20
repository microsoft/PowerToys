namespace Mages.Core.Ast.Expressions
{
    using System;

    /// <summary>
    /// The base class for all binary expressions.
    /// </summary>
    public abstract class BinaryExpression : ComputingExpression, IExpression
    {
        #region Fields

        private readonly IExpression _left;
        private readonly IExpression _right;
        private readonly String _operator;

        #endregion

        #region ctor

        /// <summary>
        /// Creates a new binary expression.
        /// </summary>
        public BinaryExpression(IExpression left, IExpression right, String op)
            : base(left.Start, right.End)
        {
            _left = left;
            _right = right;
            _operator = op;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets the value on the left side.
        /// </summary>
        public IExpression LValue 
        {
            get { return _left; }
        }

        /// <summary>
        /// Gets the value on the right side.
        /// </summary>
        public IExpression RValue
        {
            get { return _right; }
        }

        /// <summary>
        /// Gets the associated operator string.
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
        public void Validate(IValidationContext context)
        {
            if (_left is EmptyExpression)
            {
                var error = new ParseError(ErrorCode.LeftOperandRequired, LValue);
                context.Report(error);
            }

            if (_right is EmptyExpression)
            {
                var error = new ParseError(ErrorCode.RightOperandRequired, RValue);
                context.Report(error);
            }
        }

        #endregion

        #region Operations

        internal sealed class Pipe : BinaryExpression
        {
            public Pipe(IExpression left, IExpression right)
                : base(left, right, "|")
            {
            }
        }

        internal sealed class And : BinaryExpression
        {
            public And(IExpression left, IExpression right)
                : base(left, right, "&&")
            {
            }
        }

        internal sealed class Or : BinaryExpression
        {
            public Or(IExpression left, IExpression right)
                : base(left, right, "||")
            {
            }
        }

        internal sealed class Equal : BinaryExpression
        {
            public Equal(IExpression left, IExpression right)
                : base(left, right, "==")
            {
            }
        }

        internal sealed class NotEqual : BinaryExpression
        {
            public NotEqual(IExpression left, IExpression right)
                : base(left, right, "~=")
            {
            }
        }

        internal sealed class Greater : BinaryExpression
        {
            public Greater(IExpression left, IExpression right)
                : base(left, right, ">")
            {
            }
        }

        internal sealed class Less : BinaryExpression
        {
            public Less(IExpression left, IExpression right)
                : base(left, right, "<")
            {
            }
        }

        internal sealed class GreaterEqual : BinaryExpression
        {
            public GreaterEqual(IExpression left, IExpression right)
                : base(left, right, ">=")
            {
            }
        }

        internal sealed class LessEqual : BinaryExpression
        {
            public LessEqual(IExpression left, IExpression right)
                : base(left, right, "<=")
            {
            }
        }

        internal sealed class Add : BinaryExpression
        {
            public Add(IExpression left, IExpression right)
                : base(left, right, "+")
            {
            }
        }

        internal sealed class Subtract : BinaryExpression
        {
            public Subtract(IExpression left, IExpression right)
                : base(left, right, "-")
            {
            }
        }

        internal sealed class Multiply : BinaryExpression
        {
            public Multiply(IExpression left, IExpression right)
                : base(left, right, "*")
            {
            }
        }

        internal sealed class LeftDivide : BinaryExpression
        {
            public LeftDivide(IExpression left, IExpression right)
                : base(left, right, "\\")
            {
            }
        }

        internal sealed class RightDivide : BinaryExpression
        {
            public RightDivide(IExpression left, IExpression right)
                : base(left, right, "/")
            {
            }
        }

        internal sealed class Power : BinaryExpression
        {
            public Power(IExpression left, IExpression right)
                : base(left, right, "^")
            {
            }
        }

        internal sealed class Modulo : BinaryExpression
        {
            public Modulo(IExpression left, IExpression right)
                : base(left, right, "%")
            {
            }
        }

        #endregion
    }
}
