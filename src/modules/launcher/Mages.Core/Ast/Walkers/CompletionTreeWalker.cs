namespace Mages.Core.Ast.Walkers
{
    using Mages.Core.Ast.Expressions;
    using Mages.Core.Ast.Statements;
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// Represents the walker to get code completion information.
    /// </summary>
    public sealed class CompletionTreeWalker : BaseTreeWalker
    {
        #region Fields

        private readonly TextPosition _position;
        private readonly IDictionary<String, Object> _symbols;
        private readonly List<List<String>> _variables;
        private readonly List<String> _completion;
        private readonly Stack<Boolean> _breakable;

        #endregion

        #region ctor

        /// <summary>
        /// Creates a new completition tree walker for the given position.
        /// </summary>
        public CompletionTreeWalker(TextPosition position, IDictionary<String, Object> symbols)
        {
            _position = position;
            _symbols = symbols;
            _completion = new List<String>();
            _breakable = new Stack<Boolean>();
            _breakable.Push(false);
            _variables = new List<List<String>>();
            _variables.Add(new List<String>());
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets the list of autocomplete suggestions.
        /// </summary>
        public IEnumerable<String> Suggestions
        {
            get { return _completion.Where(m => !String.IsNullOrEmpty(m)).OrderBy(m => m, StringComparer.Ordinal); }
        }

        #endregion

        #region Methods

        /// <summary>
        /// Finds the suggestions for the given list of statements.
        /// </summary>
        /// <param name="statements">The statements to use.</param>
        public void FindSuggestions(IEnumerable<IStatement> statements)
        {
            foreach (var statement in statements)
            {
                statement.Accept(this);
            }

            if (_completion.Count == 0)
            {
                AddExpressionKeywords();
                AddStatementKeywords();
            }
        }

        /// <summary>
        /// Visits a block statement - accepts all childs.
        /// </summary>
        public override void Visit(BlockStatement block)
        {
            base.Visit(block);

            if (_completion.Count == 0 && Within(block))
            {
                AddExpressionKeywords();
                AddStatementKeywords();
            }
        }

        /// <summary>
        /// Visits a var statement - accepts the assignment.
        /// </summary>
        public override void Visit(VarStatement statement)
        {
            var assignment = statement.Assignment as AssignmentExpression;

            if (assignment != null)
            {
                var c = _variables.Count - 1;
                var variables = _variables[c];
                var variable = assignment.VariableName;

                if (variable != null && !variables.Contains(variable))
                {
                    variables.Add(variable);
                }
            }

            if (Within(statement))
            {
                base.Visit(statement);
            }
        }

        /// <summary>
        /// Visits a simple statement - accepts the expression.
        /// </summary>
        public override void Visit(SimpleStatement statement)
        {
            if (_completion.Count == 0 && statement.Start == _position)
            {
                AddStatementKeywords();
            }

            base.Visit(statement);
        }

        /// <summary>
        /// Visits an empty expression.
        /// </summary>
        public override void Visit(EmptyExpression expression)
        {
            if (Within(expression))
            {
                AddExpressionKeywords();
            }
        }

        /// <summary>
        /// Visits an invalid expression.
        /// </summary>
        public override void Visit(InvalidExpression expression)
        {
            if (Within(expression))
            {
                AddExpressionKeywords();
            }
        }

        /// <summary>
        /// Visits an assignment expression - accepts the variable and value.
        /// </summary>
        public override void Visit(AssignmentExpression expression)
        {
            var name = expression.VariableName;

            if (name != null)
            {
                var c = _variables.Count - 1;

                if (!_variables[c].Contains(name) && !_variables[0].Contains(name))
                {
                    _variables[0].Add(name);
                }
            }

            base.Visit(expression);
        }

        /// <summary>
        /// Visits a function expression - accepts the parameters and body.
        /// </summary>
        public override void Visit(FunctionExpression expression)
        {
            if (Within(expression))
            {
                var scope = new List<String>();
                _breakable.Push(false);
                _variables.Add(scope);
                base.Visit(expression);
                _variables.Remove(scope);
                _breakable.Pop();
            }
        }

        /// <summary>
        /// Visits a parameter expression - accepts all parameters.
        /// </summary>
        public override void Visit(ParameterExpression expression)
        {
            foreach (var parameter in expression.Parameters)
            {
                var variable = parameter as VariableExpression;

                if (variable != null)
                {
                    var variables = _variables[_variables.Count - 1];

                    if (!variables.Contains(variable.Name))
                    {
                        variables.Add(variable.Name);
                    }
                }
            }

            if (Within(expression))
            {
                _completion.Add(String.Empty);
            }
        }

        /// <summary>
        /// Visits a property expression - accepts the name and value.
        /// </summary>
        public override void Visit(PropertyExpression expression)
        {
            if (Within(expression.Name))
            {
                _completion.Add(String.Empty);
            }
            else if (Within(expression.Value))
            {
                expression.Value.Accept(this);
            }
        }

        /// <summary>
        /// Visits a variable expression.
        /// </summary>
        public override void Visit(VariableExpression expression)
        {
            if (Within(expression))
            {
                var length = _position.Index - expression.Start.Index;
                var prefix = expression.Name.Substring(0, length);
                AddExpressionKeywords(prefix);
                _completion.Add(String.Empty);
            }
        }

        /// <summary>
        /// Visits a member expression.
        /// </summary>
        public override void Visit(MemberExpression expression)
        {
            if (Within(expression.Member))
            {
                var member = expression.Member as IdentifierExpression;
                var prefix = String.Empty;
                var obj = Resolve(expression.Object);

                if (member != null)
                {
                    var length = _position.Index - member.Start.Index;
                    prefix = member.Name.Substring(0, length);
                }

                if (obj != null)
                {
                    AddSuggestions(prefix, obj.Select(m => m.Key));
                }

                _completion.Add(String.Empty);
            }
            else
            {
                base.Visit(expression);
            }
        }

        #endregion

        #region Helpers

        private IDictionary<String, Object> Resolve(IExpression expression)
        {
            var value = default(Object);
            var member = expression as MemberExpression;
            var host = _symbols;
            var name = default(String);

            if (member != null)
            {
                var child = member.Member as IdentifierExpression;

                if (child != null)
                {
                    host = Resolve(member.Object);
                    name = child.Name;
                }
            }
            else
            {
                var variable = expression as VariableExpression;

                if (variable != null)
                {
                    name = variable.Name;
                }
            }

            if (!String.IsNullOrEmpty(name) && host != null)
            {
                host.TryGetValue(name, out value);
                return value as IDictionary<String, Object>;
            }

            return null;
        }

        private void AddStatementKeywords()
        {
            _completion.AddRange(Keywords.GlobalStatementKeywords);

            if (_breakable.Peek())
            {
                _completion.AddRange(Keywords.LoopControlKeywords);
            }
        }

        private void AddExpressionKeywords()
        {
            if (_completion.Count == 0)
            {
                AddExpressionKeywords(String.Empty);
            }
        }

        private void AddExpressionKeywords(String prefix)
        {
            var symbols = _variables.
                SelectMany(m => m).
                Concat(_symbols.Keys).
                Concat(Keywords.ExpressionKeywords).
                Distinct();

            AddSuggestions(prefix, symbols);
        }

        private void AddSuggestions(String prefix, IEnumerable<String> symbols)
        {
            if (!String.IsNullOrEmpty(prefix))
            {
                symbols = symbols.Where(m => m.StartsWith(prefix)).
                    Select(m => String.Concat(prefix, "|", m.Substring(prefix.Length)));
            }

            _completion.AddRange(symbols);
        }

        private Boolean Within(ITextRange range)
        {
            return _position >= range.Start && _position <= range.End;
        }

        #endregion
    }
}
