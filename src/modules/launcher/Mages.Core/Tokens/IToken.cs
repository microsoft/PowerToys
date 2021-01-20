namespace Mages.Core.Tokens
{
    using System;

    /// <summary>
    /// Represents a token found by the tokenizer.
    /// </summary>
    public interface IToken : ITextRange
    {
        /// <summary>
        /// Gets the type of the token.
        /// </summary>
        TokenType Type { get; }

        /// <summary>
        /// Gets the payload of the token.
        /// </summary>
        String Payload { get; }
    }
}
