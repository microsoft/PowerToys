namespace Mages.Core.Ast
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// A set of useful extensions for parsing.
    /// </summary>
    public static class ParserExtensions
    {
        /// <summary>
        /// Parse the expression given in form of a string.
        /// </summary>
        /// <param name="parser">The parser.</param>
        /// <param name="code">The code to parse.</param>
        /// <returns>The resulting expression.</returns>
        public static IExpression ParseExpression(this IParser parser, String code)
        {
            var stream = code.ToTokenStream();
            return parser.ParseExpression(stream);
        }

        /// <summary>
        /// Parse the statement given in form of a string.
        /// </summary>
        /// <param name="parser">The parser.</param>
        /// <param name="code">The code to parse.</param>
        /// <returns>The resulting statement.</returns>
        public static IStatement ParseStatement(this IParser parser, String code)
        {
            var stream = code.ToTokenStream();
            return parser.ParseStatement(stream);
        }

        /// <summary>
        /// Parse the statements given in form of a string.
        /// </summary>
        /// <param name="parser">The parser.</param>
        /// <param name="code">The code to parse.</param>
        /// <returns>The resulting statements.</returns>
        public static List<IStatement> ParseStatements(this IParser parser, String code)
        {
            var stream = code.ToTokenStream();
            return parser.ParseStatements(stream);
        }
    }
}
