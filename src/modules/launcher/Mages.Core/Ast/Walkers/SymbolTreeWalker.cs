namespace Mages.Core.Ast.Walkers
{
    using Mages.Core.Ast.Expressions;
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// Represents the walker to gather symbol information.
    /// </summary>
    public sealed class SymbolTreeWalker : BaseTreeWalker
    {
        #region Fields

        private readonly IDictionary<VariableExpression, List<VariableExpression>> _collector;
        private readonly IList<VariableExpression> _missing;
        private Boolean _assigning;

        #endregion

        #region ctor

        /// <summary>
        /// Creates a new symbol tree walker.
        /// </summary>
        public SymbolTreeWalker()
            : this(new Dictionary<VariableExpression, List<VariableExpression>>(), new List<VariableExpression>())
        {
        }

        /// <summary>
        /// Creates a new symbol tree walker with a missing symbols collector.
        /// </summary>
        /// <param name="missing">The target for missing symbols.</param>
        public SymbolTreeWalker(IList<VariableExpression> missing)
            : this(new Dictionary<VariableExpression, List<VariableExpression>>(), missing)
        {
        }

        /// <summary>
        /// Creates a new symbol tree walker with a new general and missing symbols collector.
        /// </summary>
        /// <param name="collector">The target for general symbol information.</param>
        /// <param name="missing">The target for missing symbols.</param>
        public SymbolTreeWalker(IDictionary<VariableExpression, List<VariableExpression>> collector, IList<VariableExpression> missing)
        {
            _assigning = false;
            _collector = collector;
            _missing = missing;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets the found potentially missing symbols.
        /// </summary>
        public IEnumerable<VariableExpression> Missing
        {
            get { return _missing; }
        }

        /// <summary>
        /// Gets the found resolved symbols.
        /// </summary>
        public IEnumerable<VariableExpression> Symbols
        {
            get { return _collector.Keys; }
        }

        #endregion

        #region Methods

        /// <summary>
        /// Finds all references of the given symbol.
        /// </summary>
        /// <param name="symbol">The variable to get references for.</param>
        /// <returns>The list of all references of the variable.</returns>
        public IEnumerable<VariableExpression> FindAllReferences(VariableExpression symbol)
        {
            var references = default(List<VariableExpression>);
            
            if (!_collector.TryGetValue(symbol, out references))
            {
                return Enumerable.Empty<VariableExpression>();
            }

            return references;
        }

        #endregion

        #region Visitors

        /// <summary>
        /// Visits the assignment expression.
        /// </summary>
        public override void Visit(AssignmentExpression expression)
        {
            expression.Value.Accept(this);
            _assigning = true;
            expression.Variable.Accept(this);
            _assigning = false;
        }

        /// <summary>
        /// Visits the function expression.
        /// </summary>
        public override void Visit(FunctionExpression expression)
        {
            var scope = expression.Scope;
            var variables = expression.Parameters.Parameters.OfType<VariableExpression>();

            foreach (var variable in variables)
            {
                var expr = new VariableExpression(variable.Name, scope, variable.Start, variable.End);
                var list = new List<VariableExpression>();
                list.Add(expr);
                _collector.Add(expr, list);
            }

            expression.Body.Accept(this);
        }

        /// <summary>
        /// Visits the variable expression.
        /// </summary>
        public override void Visit(VariableExpression expression)
        {
            var list = Find(expression.Name, expression.Scope);

            if (list == null && _assigning)
            {
                list = new List<VariableExpression>();
                list.Add(expression);
                _collector[expression] = list;
            }
            else if (list != null)
            {
                list.Add(expression);
            }
            else
            {
                _missing.Add(expression);
            }
        }

        #endregion

        #region Helpers

        private List<VariableExpression> Find(String name, AbstractScope scope)
        {
            return Symbols.Where(m => m.Name == name && m.Scope == scope).Select(m => _collector[m]).FirstOrDefault();
        }

        #endregion
    }
}
