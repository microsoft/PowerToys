namespace Mages.Core.Ast
{
    using System;

    /// <summary>
    /// Represents the validation context.
    /// </summary>
    public interface IValidationContext
    {
        /// <summary>
        /// Gets if the current element is nested in a loop.
        /// </summary>
        Boolean IsInLoop { get; }

        /// <summary>
        /// Adds an error to the validation context.
        /// </summary>
        /// <param name="error">The error to add.</param>
        void Report(ParseError error);
    }
}
