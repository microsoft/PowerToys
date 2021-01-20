namespace Mages.Core.Ast.Walkers
{
    using Mages.Core.Ast.Expressions;
    using Mages.Core.Ast.Statements;
    using Mages.Core.Runtime.Functions;
    using Mages.Core.Vm;
    using Mages.Core.Vm.Operations;
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Represents the walker to create operations.
    /// </summary>
    public sealed class OperationTreeWalker : ITreeWalker, IValidationContext
    {
        #region Operator Mappings

        private static readonly Dictionary<String, Action<OperationTreeWalker, PreUnaryExpression>> PreUnaryOperatorMapping = new Dictionary<String, Action<OperationTreeWalker, PreUnaryExpression>>
        {
            { "~", (walker, expr) => walker.Handle(expr, StandardOperators.Not) },
            { "+", (walker, expr) => walker.Handle(expr, StandardOperators.Positive) },
            { "-", (walker, expr) => walker.Handle(expr, StandardOperators.Negative) },
            { "&", (walker, expr) => walker.Handle(expr, StandardOperators.Type) },
            { "++", (walker, expr) => walker.Place(IncOperation.Instance, expr.Value, false) },
            { "--", (walker, expr) => walker.Place(DecOperation.Instance, expr.Value, false) }
        };

        private static readonly Dictionary<String, Action<OperationTreeWalker, PostUnaryExpression>> PostUnaryOperatorMapping = new Dictionary<String, Action<OperationTreeWalker, PostUnaryExpression>>
        {
            { "!", (walker, expr) => walker.Handle(expr, StandardOperators.Factorial) },
            { "'", (walker, expr) => walker.Handle(expr, StandardOperators.Transpose) },
            { "++", (walker, expr) => walker.Place(IncOperation.Instance, expr.Value, true) },
            { "--", (walker, expr) => walker.Place(DecOperation.Instance, expr.Value, true) }
        };

        private static readonly Dictionary<String, Action<OperationTreeWalker, BinaryExpression>> BinaryOperatorMapping = new Dictionary<String, Action<OperationTreeWalker, BinaryExpression>>
        {
            { "&&", (walker, expr) => walker.Handle(expr, StandardOperators.And) },
            { "||", (walker, expr) => walker.Handle(expr, StandardOperators.Or) },
            { "==", (walker, expr) => walker.Handle(expr, StandardOperators.Eq) },
            { "~=", (walker, expr) => walker.Handle(expr, StandardOperators.Neq) },
            { ">", (walker, expr) => walker.Handle(expr, StandardOperators.Gt) },
            { "<", (walker, expr) => walker.Handle(expr, StandardOperators.Lt) },
            { ">=", (walker, expr) => walker.Handle(expr, StandardOperators.Geq) },
            { "<=", (walker, expr) => walker.Handle(expr, StandardOperators.Leq) },
            { "+", (walker, expr) => walker.Handle(expr, StandardOperators.Add) },
            { "-", (walker, expr) => walker.Handle(expr, StandardOperators.Sub) },
            { "*", (walker, expr) => walker.Handle(expr, StandardOperators.Mul) },
            { "/", (walker, expr) => walker.Handle(expr, StandardOperators.RDiv) },
            { "\\", (walker, expr) => walker.Handle(expr, StandardOperators.LDiv) },
            { "^", (walker, expr) => walker.Handle(expr, StandardOperators.Pow) },
            { "%", (walker, expr) => walker.Handle(expr, StandardOperators.Mod) },
            { "|", (walker, expr) => walker.Handle(expr, StandardOperators.Pipe) }
        };

        #endregion

        #region Fields

        private readonly List<IOperation> _operations;
        private readonly Stack<LoopInfo> _loops;
        private Boolean _assigning;
        private Boolean _declaring;
        private Boolean _member;

        #endregion

        #region ctor

        /// <summary>
        /// Creates a new operation tree walker.
        /// </summary>
        /// <param name="operations">The list of operations to populate.</param>
        public OperationTreeWalker(List<IOperation> operations)
        {
            _operations = operations;
            _loops = new Stack<LoopInfo>();
            _assigning = false;
            _declaring = false;
        }

        #endregion

        #region Properties

        Boolean IValidationContext.IsInLoop
        {
            get { return _loops.Count > 0 && _loops.Peek().Break != 0; }
        }

        #endregion

        #region Visitors

        void ITreeWalker.Visit(BlockStatement block)
        {
            block.Validate(this);
            var isLoop = _loops.Count > 0;

            foreach (var statement in block.Statements)
            {
                statement.Accept(this);

                if (isLoop && statement is SimpleStatement)
                {
                    _operations.Add(PopOperation.Instance);
                }
            }
        }

        void ITreeWalker.Visit(SimpleStatement statement)
        {
            statement.Validate(this);
            statement.Expression.Accept(this);
        }

        void ITreeWalker.Visit(VarStatement statement)
        {
            _declaring = true;
            statement.Validate(this);
            statement.Assignment.Accept(this);
            _declaring = false;
        }

        void ITreeWalker.Visit(ReturnStatement statement)
        {
            statement.Validate(this);
            statement.Expression.Accept(this);
            _operations.Add(RetOperation.Instance);
        }

        void ITreeWalker.Visit(DeleteExpression expression)
        {
            expression.Validate(this);
            var member = expression.Payload as MemberExpression;

            if (member != null)
            {
                var variable = member.Member as IdentifierExpression;
                member.Validate(this);

                if (variable != null)
                {
                    member.Object.Accept(this);
                    variable.Validate(this);
                    var op = new DelKeyOperation(variable.Name);
                    _operations.Add(op);
                }
            }
            else
            {
                var variable = expression.Payload as VariableExpression;

                if (variable != null)
                {
                    variable.Validate(this);
                    var op = new DelVarOperation(variable.Name);
                    _operations.Add(op);
                }
            }
        }

        void ITreeWalker.Visit(WhileStatement statement)
        {
            statement.Validate(this);
            var start = _operations.Count;
            statement.Condition.Accept(this);
            _operations.Add(PopIfOperation.Instance);
            var jump = InsertMarker();
            _loops.Push(new LoopInfo { Break = jump, Continue = start });
            statement.Body.Accept(this);
            _loops.Pop();
            InsertJump(start - 1);
            var end = _operations.Count;
            InsertJump(jump, end - 1);
        }

        void ITreeWalker.Visit(ForStatement statement)
        {
            statement.Validate(this);
            _declaring = statement.IsDeclared;
            statement.Initialization.Accept(this);
            _declaring = false;
            var start = _operations.Count;
            statement.Condition.Accept(this);
            _operations.Add(PopIfOperation.Instance);
            var jump = InsertMarker();
            _loops.Push(new LoopInfo { Break = jump, Continue = start });
            statement.Body.Accept(this);
            statement.AfterThought.Accept(this);
            _loops.Pop();
            InsertJump(start - 1);
            var end = _operations.Count;
            InsertJump(jump, end - 1);
        }

        void ITreeWalker.Visit(IfStatement statement)
        {
            statement.Validate(this);
            statement.Condition.Accept(this);
            _operations.Add(PopIfOperation.Instance);
            var jumpToElse = InsertMarker();
            statement.Primary.Accept(this);
            var jumpToEnd = InsertMarker();
            statement.Secondary.Accept(this);
            var end = _operations.Count;
            InsertJump(jumpToElse, jumpToEnd);
            InsertJump(jumpToEnd, end - 1);
        }

        void ITreeWalker.Visit(MatchStatement statement)
        {
            var name = "^~#" + _loops.Count;
            statement.Validate(this);
            statement.Reference.Accept(this);
            _operations.Add(new SetsOperation(name));
            InsertJump(_operations.Count + 1);
            var jumpToEnd = InsertMarker();
            _loops.Push(new LoopInfo { Break = jumpToEnd, Continue = jumpToEnd });
            statement.Cases.Accept(this);
            _loops.Pop();
            var end = _operations.Count;
            _operations.Add(new DelVarOperation(name));
            _operations.Add(PopOperation.Instance);
            InsertJump(jumpToEnd, end - 1);
        }

        void ITreeWalker.Visit(CaseStatement statement)
        {
            var loop = _loops.Pop();
            statement.Validate(this);
            _operations.Add(new GetsOperation("^~#" + _loops.Count));
            statement.Condition.Accept(this);
            _operations.Add(new GetcOperation(1));
            _operations.Add(PopIfOperation.Instance);
            var jumpToNext = InsertMarker();
            _loops.Push(new LoopInfo { Break = loop.Break, Continue = jumpToNext });
            statement.Body.Accept(this);
            var end = _operations.Count;
            InsertJump(jumpToNext, end - 1);
        }

        void ITreeWalker.Visit(BreakStatement statement)
        {
            statement.Validate(this);
            var position = _loops.Peek().Break;
            InsertJump(position - 1);
        }

        void ITreeWalker.Visit(ContinueStatement statement)
        {
            statement.Validate(this);
            var position = _loops.Peek().Continue;
            InsertJump(position - 1);
        }

        void ITreeWalker.Visit(EmptyExpression expression)
        {
            expression.Validate(this);
            _operations.Add(ConstOperation.Null);
        }

        void ITreeWalker.Visit(ConstantExpression expression)
        {
            var constant = expression.Value;
            expression.Validate(this);
            _operations.Add(new ConstOperation(constant));
        }

        void ITreeWalker.Visit(AwaitExpression expression)
        {
            expression.Validate(this);
            expression.Payload.Accept(this);
            _operations.Add(AwaitOperation.Instance);
        }

        void ITreeWalker.Visit(ArgumentsExpression expression)
        {
            var arguments = expression.Arguments;
            expression.Validate(this);

            for (var i = arguments.Length - 1; i >= 0; i--)
            {
                arguments[i].Accept(this);
            }
        }

        void ITreeWalker.Visit(AssignmentExpression expression)
        {
            _assigning = true;
            expression.Validate(this);
            expression.Variable.Accept(this);
            _assigning = false;
            var store = ExtractLast();
            expression.Value.Accept(this);
            _operations.Add(store);
        }

        void ITreeWalker.Visit(BinaryExpression expression)
        {
            var action = default(Action<OperationTreeWalker, BinaryExpression>);
            expression.Validate(this);
            BinaryOperatorMapping.TryGetValue(expression.Operator, out action);
            action.Invoke(this, expression);
        }

        void ITreeWalker.Visit(PreUnaryExpression expression)
        {
            var action = default(Action<OperationTreeWalker, PreUnaryExpression>);
            expression.Validate(this);
            PreUnaryOperatorMapping.TryGetValue(expression.Operator, out action);
            action.Invoke(this, expression);
        }

        void ITreeWalker.Visit(PostUnaryExpression expression)
        {
            var action = default(Action<OperationTreeWalker, PostUnaryExpression>);
            expression.Validate(this);
            PostUnaryOperatorMapping.TryGetValue(expression.Operator, out action);
            action.Invoke(this, expression);
        }

        void ITreeWalker.Visit(RangeExpression expression)
        {
            var hasStep = expression.Step is EmptyExpression == false;
            var operation = RngiOperation.Instance;
            expression.Validate(this);

            if (hasStep)
            {
                operation = RngeOperation.Instance;
                expression.Step.Accept(this);
            }

            expression.To.Accept(this);
            expression.From.Accept(this);
            _operations.Add(operation);
        }

        void ITreeWalker.Visit(ConditionalExpression expression)
        {
            expression.Validate(this);
            expression.Secondary.Accept(this);
            expression.Primary.Accept(this);
            expression.Condition.Accept(this);

            _operations.Add(CondOperation.Instance);
        }

        void ITreeWalker.Visit(CallExpression expression)
        {
            var assigning = _assigning;
            _assigning = false;

            expression.Validate(this);
            expression.Arguments.Accept(this);
            expression.Function.Accept(this);

            if (assigning)
            {
                _operations.Add(new SetcOperation(expression.Arguments.Count));
            }
            else
            {
                _operations.Add(new GetcOperation(expression.Arguments.Count));
            }

            _assigning = assigning;
        }

        void ITreeWalker.Visit(ObjectExpression expression)
        {
            expression.Validate(this);
            _operations.Add(NewObjOperation.Instance);

            foreach (var property in expression.Values)
            {
                property.Accept(this);
            }
        }

        void ITreeWalker.Visit(PropertyExpression expression)
        {
            expression.Validate(this);
            expression.Name.Accept(this);
            _member = true;
            expression.Value.Accept(this);
            _member = false;
            _operations.Add(InitObjOperation.Instance);
        }

        void ITreeWalker.Visit(MatrixExpression expression)
        {
            var values = expression.Values;
            var rows = values.Length;
            var cols = rows > 0 ? values[0].Length : 0;
            expression.Validate(this);
            _operations.Add(new NewMatOperation(rows, cols));

            for (var row = 0; row < rows; row++)
            {
                for (var col = 0; col < cols; col++)
                {
                    var value = values[row][col];
                    value.Accept(this);
                    _operations.Add(new InitMatOperation(row, col));
                }
            }
        }

        void ITreeWalker.Visit(FunctionExpression expression)
        {
            var member = _member;
            var current = _operations.Count;
            var parameters = expression.Parameters;
            _member = false;
            expression.Validate(this);
            parameters.Accept(this);
            _loops.Push(default(LoopInfo));
            expression.Body.Accept(this);
            _loops.Pop();
            var function = ExtractFunction(member, parameters.Names, current);
            _operations.Add(function);
        }

        void ITreeWalker.Visit(InvalidExpression expression)
        {
            expression.Validate(this);
        }

        void ITreeWalker.Visit(IdentifierExpression expression)
        {
            var name = expression.Name;
            expression.Validate(this);
            _operations.Add(new ConstOperation(name));
        }

        void ITreeWalker.Visit(MemberExpression expression)
        {
            var assigning = _assigning;
            _assigning = false;

            expression.Validate(this);
            expression.Member.Accept(this);
            expression.Object.Accept(this);

            if (assigning)
            {
                _operations.Add(SetpOperation.Instance);
            }
            else
            {
                _operations.Add(GetpOperation.Instance);
            }

            _assigning = assigning;
        }

        void ITreeWalker.Visit(ParameterExpression expression)
        {
            var expressions = expression.Parameters;
            expression.Validate(this);

            for (var i = 0; i < expressions.Length; i++)
            {
                var identifier = expressions[i] as VariableExpression;
                
                if (identifier == null)
                {
                    var assignment = expressions[i] as AssignmentExpression;
                    identifier = (VariableExpression)assignment.Variable;
                    _operations.Add(new ArgcOperation(i));
                    _operations.Add(PopIfOperation.Instance);
                    var marker = InsertMarker();
                    assignment.Value.Accept(this);
                    _operations.Add(new ArgoOperation(i));
                    var end = _operations.Count;
                    InsertJump(marker, end - 1);
                }

                var name = identifier.Name;
                _operations.Add(new ArgOperation(i, name));
            }

            _operations.Add(ArgsOperation.Instance);
        }

        void ITreeWalker.Visit(VariableExpression expression)
        {
            var name = expression.Name;
            expression.Validate(this);

            if (_assigning)
            {
                if (_declaring)
                {
                    _operations.Add(new DefOperation(name));
                }
                else
                {
                    _operations.Add(new SetsOperation(name));
                }
            }
            else
            {
                _operations.Add(new GetsOperation(name));
            }
        }

        void ITreeWalker.Visit(InterpolatedExpression expression)
        {
            var replacements = expression.Replacements;
            var length = replacements.Length + 1;
            expression.Validate(this);

            for (var i = replacements.Length - 1; i >= 0; i--)
            {
                replacements[i].Accept(this);
            }

            expression.Format.Accept(this);
            CallFunction(StandardFunctions.Format, length);
        }

        #endregion

        #region Error Reporting

        void IValidationContext.Report(ParseError error)
        {
            throw new ParseException(error);
        }

        #endregion

        #region Helpers

        private void InsertJump(Int32 target)
        {
            var index = _operations.Count;
            _operations.Add(new JumpOperation(target - index));
        }

        private void InsertJump(Int32 index, Int32 target)
        {
            _operations[index] = new JumpOperation(target - index);
        }

        private Int32 InsertMarker()
        {
            var marker = _operations.Count;
            _operations.Add(null);
            return marker;
        }

        private void Handle(BinaryExpression expression, Function function)
        {
            expression.LValue.Accept(this);
            expression.RValue.Accept(this);
            CallFunction(function, 2);
        }

        private void Handle(PreUnaryExpression expression, Function function)
        {
            expression.Value.Accept(this);
            CallFunction(function, 1);
        }

        private void Handle(PostUnaryExpression expression, Function function)
        {
            expression.Value.Accept(this);
            CallFunction(function, 1);
        }

        private void CallFunction(Function func, Int32 argumentCount)
        {
            _operations.Add(new ConstOperation(func));
            _operations.Add(new GetcOperation(argumentCount));
        }

        private IOperation ExtractLast()
        {
            var index = _operations.Count - 1;
            var operation = _operations[index];
            _operations.RemoveAt(index);
            return operation;
        }

        private IOperation ExtractFunction(Boolean member, ParameterDefinition[] parameters, Int32 index)
        {
            var operations = ExtractFrom(index);

            if (member)
            {
                return new NewMethOperation(parameters, operations);
            }

            return new NewFuncOperation(parameters, operations);
        }

        private IOperation[] ExtractFrom(Int32 index)
        {
            var count = _operations.Count;
            var length = count - index;
            var operations = new IOperation[length];

            while (count > index)
            {
                operations[--length] = _operations[--count];
                _operations.RemoveAt(count);
            }

            return operations;
        }

        private void Place(IOperation operation, IExpression expr, Boolean postOperation)
        {
            if (postOperation)
            {
                expr.Accept(this);
            }

            _assigning = true;
            expr.Accept(this);
            _assigning = false;
            var store = ExtractLast();
            expr.Accept(this);
            _operations.Add(operation);
            _operations.Add(store);

            if (postOperation)
            {
                _operations.Add(PopOperation.Instance);
            }
        }

        #endregion

        #region Loop

        struct LoopInfo
        {
            public Int32 Continue;
            public Int32 Break;
        }

        #endregion
    }
}
