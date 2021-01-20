namespace Mages.Core.Source
{
    using System;

    /// <summary>
    /// Represents the source code scanner.
    /// </summary>
    public interface IScanner : IDisposable
    {
        /// <summary>
        /// Gets the current character code.
        /// </summary>
        Int32 Current { get; }

        /// <summary>
        /// Gets the current position in the source code.
        /// </summary>
        TextPosition Position { get; }

        /// <summary>
        /// Tries to move to the next position.
        /// </summary>
        /// <returns>True if the next character exists, otherwise false.</returns>
        Boolean MoveNext();

        /// <summary>
        /// Tries to move to the previous position.
        /// </summary>
        /// <returns>True if the previous character exists, otherwise false.</returns>
        Boolean MoveBack();

        /// <summary>
        /// Gets the position at the given index.
        /// </summary>
        /// <param name="index">The linear index in the source.</param>
        /// <returns>The corresponding text position.</returns>
        TextPosition GetPositionAt(Int32 index);
    }
}
