namespace Mages.Core.Tokens
{
    using Mages.Core.Source;

    /// <summary>
    /// Represents the tokenizer performing the lexical analysis.
    /// </summary>
    public interface ITokenizer
    {
        /// <summary>
        /// Gets the next token from the scanner.
        /// </summary>
        /// <returns>The token.</returns>
        IToken Next(IScanner scanner);
    }
}
