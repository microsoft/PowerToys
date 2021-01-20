namespace Mages.Core.Vm
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Represents the model of the MAGES VM.
    /// </summary>
    public interface IExecutionContext
    {
        /// <summary>
        /// Gets or sets the position of the previous operation.
        /// </summary>
        Int32 Position { get; set; }

        /// <summary>
        /// Gets the position of the last operation.
        /// </summary>
        Int32 End { get; }

        /// <summary>
        /// Gets the currently used execution scope.
        /// </summary>
        IDictionary<String, Object> Scope { get; }

        /// <summary>
        /// Pushes a new value on the stack.
        /// </summary>
        /// <param name="value">The value to push on the stack.</param>
        void Push(Object value);

        /// <summary>
        /// Pops a value from the stack.
        /// </summary>
        /// <returns>The value that came from the stack.</returns>
        Object Pop();
    }
}
