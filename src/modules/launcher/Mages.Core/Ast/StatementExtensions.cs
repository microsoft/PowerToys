namespace Mages.Core.Ast
{
    using Mages.Core.Ast.Expressions;
    using Mages.Core.Ast.Statements;
    using Mages.Core.Ast.Walkers;
    using Mages.Core.Vm;
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// A collection of statement extensions.
    /// </summary>
    public static class StatementExtensions
    {
        /// <summary>
        /// Looks for missing symbols in the provided statement.
        /// </summary>
        /// <param name="statement">The statement.</param>
        /// <returns>The found list of missing symbols.</returns>
        public static List<VariableExpression> FindMissingSymbols(this IStatement statement)
        {
            var missingSymbols = new List<VariableExpression>();
            statement.CollectMissingSymbols(missingSymbols);
            return missingSymbols;
        }

        /// <summary>
        /// Looks for missing symbols in the provided statements.
        /// </summary>
        /// <param name="statements">The statements.</param>
        /// <returns>The found list of missing symbols.</returns>
        public static List<VariableExpression> FindMissingSymbols(this IEnumerable<IStatement> statements)
        {
            var block = statements.ToBlock();
            return block.FindMissingSymbols();
        }

        /// <summary>
        /// Converts the given statements to a single block statement.
        /// </summary>
        /// <param name="statements">The statements.</param>
        /// <returns>The single block statement containing all statements.</returns>
        public static BlockStatement ToBlock(this IEnumerable<IStatement> statements)
        {
            var list = new List<IStatement>(statements);
            var start = list.Count > 0 ? list[0].Start : new TextPosition();
            var end = list.Count > 0 ? list[list.Count - 1].End : start;
            return new BlockStatement(list.ToArray(), start, end);
        }

        /// <summary>
        /// Collects the missing symbols in the provided statement.
        /// </summary>
        /// <param name="statement">The statement.</param>
        /// <param name="missingSymbols">The list of missing symbols to populate.</param>
        public static void CollectMissingSymbols(this IStatement statement, List<VariableExpression> missingSymbols)
        {
            var walker = new SymbolTreeWalker(missingSymbols);
            statement.Accept(walker);
        }

        /// <summary>
        /// Transforms the statements to an array of operations.
        /// </summary>
        /// <param name="statements">The statements.</param>
        /// <returns>The operations that can be run.</returns>
        public static IOperation[] MakeRunnable(this IEnumerable<IStatement> statements)
        {
            var operations = new List<IOperation>();
            var walker = new OperationTreeWalker(operations);
            statements.ToBlock().Accept(walker);
            return operations.ToArray();
        }

        /// <summary>
        /// Checks if the given statement is a simple statement containing
        /// an empty expression.
        /// </summary>
        /// <param name="statement">The statement.</param>
        /// <returns>True if the statement is empty, otherwise false.</returns>
        public static Boolean IsEmpty(this IStatement statement)
        {
            var simple = statement as SimpleStatement;
            return simple != null && simple.Expression.IsEmpty();
        }

        /// <summary>
        /// Checks if the given expression is an empty expression.
        /// </summary>
        /// <param name="expression">The expression.</param>
        /// <returns>True if the expression is empty, otherwise false.</returns>
        public static Boolean IsEmpty(this IExpression expression)
        {
            return expression is EmptyExpression;
        }

        /// <summary>
        /// Gets the list of possible completions at the given position.
        /// </summary>
        /// <param name="statements">The statements.</param>
        /// <param name="position">The position to look for completions.</param>
        /// <param name="symbols">The existing global symbols.</param>
        /// <returns>The list of completions for the given position.</returns>
        public static IEnumerable<String> GetCompletionAt(this IEnumerable<IStatement> statements, TextPosition position, IDictionary<String, Object> symbols)
        {
            var walker = new CompletionTreeWalker(position, symbols);
            walker.FindSuggestions(statements);
            return walker.Suggestions;
        }
    }
}
