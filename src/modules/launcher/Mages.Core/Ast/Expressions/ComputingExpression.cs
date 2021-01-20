namespace Mages.Core.Ast.Expressions
{
    using System;

    /// <summary>
    /// Represents a computed expression (not-assignable).
    /// </summary>
    public abstract class ComputingExpression : BaseExpression
    {
        #region ctor

        /// <summary>
        /// Creates a new computing expression.
        /// </summary>
        public ComputingExpression(TextPosition start, TextPosition end)
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
            get { return false; }
        }

        #endregion
    }
}
