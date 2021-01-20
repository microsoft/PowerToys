namespace Mages.Core.Ast.Expressions
{
    using System;

    /// <summary>
    /// Represents an expression that can be assigned.
    /// </summary>
    public abstract class AssignableExpression : BaseExpression
    {
        #region ctor

        /// <summary>
        /// Creates a new assignable expression.
        /// </summary>
        public AssignableExpression(TextPosition start, TextPosition end)
            : base(start, end)
        {
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets if the expression is assignable.
        /// </summary>
        public Boolean IsAssignable
        {
            get { return true; }
        }

        #endregion
    }
}
