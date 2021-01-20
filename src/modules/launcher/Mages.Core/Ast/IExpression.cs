namespace Mages.Core.Ast
{
    using System;

    /// <summary>
    /// An abstract expression from the AST.
    /// </summary>
    public interface IExpression : IValidatable, IWalkable
    {
        /// <summary>
        /// Gets if the expression can be used as a value container.
        /// </summary>
        Boolean IsAssignable { get; }
    }
}
