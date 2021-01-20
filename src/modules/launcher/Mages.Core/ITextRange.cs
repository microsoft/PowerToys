namespace Mages.Core
{
    /// <summary>
    /// Represents a range of characters within the source code.
    /// </summary>
    public interface ITextRange
    {
        /// <summary>
        /// Gets the start position of the token.
        /// </summary>
        TextPosition Start { get; }

        /// <summary>
        /// Gets the end position of the token.
        /// </summary>
        TextPosition End { get; }
    }
}
