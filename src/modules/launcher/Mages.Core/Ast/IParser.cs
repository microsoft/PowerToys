namespace Mages.Core.Ast
{
    using Mages.Core.Tokens;
    using System.Collections.Generic;

    /// <summary>
    /// Represents the core parser interface.
    /// </summary>
    public interface IParser
    {
        /// <summary>
        /// Parses the next expression.
        /// </summary>
        /// <param name="tokens">The stream of tokens.</param>
        /// <returns>The parsed expression.</returns>
        IExpression ParseExpression(IEnumerator<IToken> tokens);

        /// <summary>
        /// Parses the next statement.
        /// </summary>
        /// <param name="tokens">The stream of tokens.</param>
        /// <returns>The parsed statement.</returns>
        IStatement ParseStatement(IEnumerator<IToken> tokens);
        
        /// <summary>
        /// Parse the next statements.
        /// </summary>
        /// <param name="tokens">The stream of tokens.</param>
        /// <returns>The parsed statements.</returns>
        List<IStatement> ParseStatements(IEnumerator<IToken> tokens);
    }
}
