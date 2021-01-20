namespace Mages.Core.Ast
{
    using Mages.Core.Ast.Expressions;
    using Mages.Core.Ast.Statements;
    using Mages.Core.Tokens;
    using System;
    using System.Collections.Generic;

    sealed class ExpressionParser : IParser
    {
        #region Fields

        private readonly AbstractScopeStack _scopes;

        #endregion

        #region ctor

        public ExpressionParser()
        {
            var root = new AbstractScope(null);
            _scopes = new AbstractScopeStack(root);
        }

        #endregion

        #region Methods

        public List<IStatement> ParseStatements(IEnumerator<IToken> tokens)
        {
            return ParseNextStatements(tokens.NextNonIgnorable());
        }

        public IStatement ParseStatement(IEnumerator<IToken> tokens)
        {
            return ParseNextStatement(tokens.NextNonIgnorable());
        }

        public IExpression ParseExpression(IEnumerator<IToken> tokens)
        {
            var expr = ParseAssignment(tokens.NextNonIgnorable());

            if (tokens.Current.Type != TokenType.End)
            {
                var invalid = ParseInvalid(ErrorCode.InvalidSymbol, tokens);
                expr = new BinaryExpression.Multiply(expr, invalid);
            }

            return expr;
        }

        #endregion

        #region Statements

        private List<IStatement> ParseNextStatements(IEnumerator<IToken> tokens)
        {
            var statements = new List<IStatement>();

            while (tokens.Current.Type != TokenType.End)
            {
                var statement = ParseNextStatement(tokens);
                statements.Add(statement);
            }

            return statements;
        }

        private IStatement ParseNextStatement(IEnumerator<IToken> tokens)
        {
            var current = tokens.Current;

            if (current.Type == TokenType.Keyword)
            {
                return ParseKeywordStatement(tokens);
            }
            else if (current.Type == TokenType.OpenScope)
            {
                return ParseBlockStatement(tokens);
            }

            return ParseSimpleStatement(tokens);
        }

        private IStatement ParseKeywordStatement(IEnumerator<IToken> tokens)
        {
            var current = tokens.Current;

            if (current.Is(Keywords.Var))
            {
                return ParseVarStatement(tokens);
            }
            else if (current.Is(Keywords.Return))
            {
                return ParseReturnStatement(tokens);
            }
            else if (current.Is(Keywords.While))
            {
                return ParseWhileStatement(tokens);
            }
            else if (current.Is(Keywords.If))
            {
                return ParseIfStatement(tokens);
            }
            else if (current.Is(Keywords.For))
            {
                return ParseForStatement(tokens);
            }
            else if (current.Is(Keywords.Break))
            {
                return ParseBreakStatement(tokens);
            }
            else if (current.Is(Keywords.Continue))
            {
                return ParseContinueStatement(tokens);
            }
            else if (current.Is(Keywords.Match))
            {
                return ParseMatchStatement(tokens);
            }

            return ParseSimpleStatement(tokens);
        }

        private SimpleStatement ParseSimpleStatement(IEnumerator<IToken> tokens)
        {
            var expr = ParseAssignment(tokens);
            var end = tokens.Current.End;
            CheckProperStatementEnd(tokens, ref expr);
            return new SimpleStatement(expr, end);
        }

        private IStatement ParseMatchStatement(IEnumerator<IToken> tokens)
        {
            var start = tokens.Current.Start;
            var condition = ParseCondition(tokens.NextNonIgnorable());
            var block = ParseAfterMatch(tokens);
            return new MatchStatement(condition, block, start);
        }

        private IStatement ParseIfStatement(IEnumerator<IToken> tokens)
        {
            var start = tokens.Current.Start;
            var condition = ParseCondition(tokens.NextNonIgnorable());
            var primary = ParseAfterCondition(tokens);
            var secondary = default(IStatement);

            if (tokens.Current.Is(Keywords.Else))
            {
                secondary = ParseExpectedStatement(tokens.NextNonIgnorable());
            }
            else
            {
                secondary = new BlockStatement(new IStatement[0], primary.End, primary.End);
            }

            return new IfStatement(condition, primary, secondary, start);
        }

        private IStatement ParseWhileStatement(IEnumerator<IToken> tokens)
        {
            var start = tokens.Current.Start;
            var condition = ParseCondition(tokens.NextNonIgnorable());
            var body = ParseAfterCondition(tokens);
            return new WhileStatement(condition, body, start);
        }

        private IStatement ParseForStatement(IEnumerator<IToken> tokens)
        {
            var start = tokens.Current.Start;
            var declared = false;
            var initialization = ParseInitialization(tokens.NextNonIgnorable(), ref declared);
            var condition = ParseAssignment(tokens);
            CheckProperlyTerminated(tokens, ref condition);
            var afterthought = ParseAfterthought(tokens);
            var body = ParseAfterCondition(tokens);
            return new ForStatement(declared, initialization, condition, afterthought, body, start);
        }

        private IStatement ParseReturnStatement(IEnumerator<IToken> tokens)
        {
            var start = tokens.Current.Start;
            var expr = ParseAssignment(tokens.NextNonIgnorable());
            var end = tokens.Current.End;
            CheckProperStatementEnd(tokens, ref expr);
            return new ReturnStatement(expr, start, end);
        }

        private IStatement ParseContinueStatement(IEnumerator<IToken> tokens)
        {
            var start = tokens.Current.Start;
            var expr = ParseAssignment(tokens.NextNonIgnorable());
            var end = tokens.Current.End;
            CheckProperStatementEnd(tokens, ref expr);
            return new ContinueStatement(expr, start, end);
        }

        private IStatement ParseBreakStatement(IEnumerator<IToken> tokens)
        {
            var start = tokens.Current.Start;
            var expr = ParseAssignment(tokens.NextNonIgnorable());
            var end = tokens.Current.End;
            CheckProperStatementEnd(tokens, ref expr);
            return new BreakStatement(expr, start, end);
        }

        private IStatement ParseVarStatement(IEnumerator<IToken> tokens)
        {
            var start = tokens.Current.Start;
            var expr = ParseAssignment(tokens.NextNonIgnorable());
            var end = tokens.Current.End;
            CheckProperStatementEnd(tokens, ref expr);
            return new VarStatement(expr, start, end);
        }

        private BlockStatement ParseBlockStatement(IEnumerator<IToken> tokens)
        {
            var start = tokens.Current.Start;
            var current = tokens.NextNonIgnorable().Current;
            var statements = new List<IStatement>();

            while (current.Type != TokenType.CloseScope)
            {
                if (current.Type == TokenType.End)
                {
                    var invalid = new InvalidExpression(ErrorCode.BlockNotTerminated, current);
                    var statement = new SimpleStatement(invalid, current.End);
                    statements.Add(statement);
                    break;
                }

                statements.Add(ParseNextStatement(tokens));
                current = tokens.Current;
            }

            var end = tokens.Current.End;
            tokens.NextNonIgnorable();
            return new BlockStatement(statements.ToArray(), start, end);
        }

        private IStatement ParseAfterCondition(IEnumerator<IToken> tokens)
        {
            var token = tokens.Current;

            if (token.Type != TokenType.CloseGroup)
            {
                var invalid = new InvalidExpression(ErrorCode.BracketNotTerminated, token);
                return new SimpleStatement(invalid, token.End);
            }

            return ParseExpectedStatement(tokens.NextNonIgnorable());
        }

        private IStatement ParseAfterMatch(IEnumerator<IToken> tokens)
        {
            var token = tokens.Current;

            if (token.Type != TokenType.CloseGroup)
            {
                var invalid = new InvalidExpression(ErrorCode.BracketNotTerminated, token);
                return new SimpleStatement(invalid, token.End);
            }
            
            token = tokens.NextNonIgnorable().Current;

            if (token.Type == TokenType.End)
            {
                var invalid = new InvalidExpression(ErrorCode.StatementExpected, token);
                return new SimpleStatement(invalid, token.End);
            }
            else if (token.Type != TokenType.OpenScope)
            {
                var invalid = new InvalidExpression(ErrorCode.BracketExpected, token);
                return new SimpleStatement(invalid, token.End);
            }

            var start = tokens.Current.Start;
            token = tokens.NextNonIgnorable().Current;
            var statements = new List<IStatement>();

            while (token.Type != TokenType.CloseScope)
            {
                if (token.Type == TokenType.End)
                {
                    var invalid = new InvalidExpression(ErrorCode.BlockNotTerminated, token);
                    var statement = new SimpleStatement(invalid, token.End);
                    statements.Add(statement);
                    break;
                }
                else
                {
                    var expr = ParsePrimary(tokens);
                    var block = ParseBlockStatement(tokens);
                    var stmt = new CaseStatement(expr, block);
                    statements.Add(stmt);
                }

                token = tokens.Current;
            }

            var end = tokens.Current.End;
            tokens.NextNonIgnorable();
            return new BlockStatement(statements.ToArray(), start, end);
        }

        private IStatement ParseExpectedStatement(IEnumerator<IToken> tokens)
        {
            var token = tokens.Current;

            if (token.Type == TokenType.End)
            {
                var invalid = new InvalidExpression(ErrorCode.StatementExpected, token);
                return new SimpleStatement(invalid, token.End);
            }

            return ParseNextStatement(tokens);
        }

        #endregion

        #region Expressions

        private List<IExpression> ParseExpressions(IEnumerator<IToken> tokens)
        {
            var expressions = new List<IExpression>();
            var token = tokens.Current;
            var allowNext = true;

            while (allowNext && token.Type != TokenType.End)
            {
                var expression = ParseAssignment(tokens);
                allowNext = false;
                expressions.Add(expression);
                token = tokens.Current;

                if (token.Type == TokenType.Comma)
                {
                    allowNext = true;
                    token = tokens.NextNonIgnorable().Current;

                    while (token.Type == TokenType.Comma)
                    {
                        var invalid = ParseInvalid(ErrorCode.ExpressionExpected, tokens);
                        expressions.Add(invalid);
                        token = tokens.Current;
                    }
                }
            }

            return expressions;
        }

        private FunctionExpression ParseFunction(ParameterExpression parameters, IEnumerator<IToken> tokens)
        {
            var body = default(IStatement);
            _scopes.PushNew();

            if (tokens.Current.Type == TokenType.OpenScope)
            {
                body = ParseBlockStatement(tokens);
            }
            else
            {
                var expr = ParseAssignment(tokens);
                body = new SimpleStatement(expr, expr.End);
            }

            var scope = _scopes.PopCurrent();
            return new FunctionExpression(scope, parameters, body);
        }

        private IExpression ParseCondition(IEnumerator<IToken> tokens)
        {
            var current = tokens.Current;

            if (current.Type == TokenType.OpenGroup)
            {
                if (tokens.NextNonIgnorable().Current.Type == TokenType.CloseGroup)
                {
                    return new EmptyExpression(current.End);
                }

                return ParseAssignment(tokens);
            }

            return new InvalidExpression(ErrorCode.OpenGroupExpected, current);
        }

        private IExpression ParseAfterthought(IEnumerator<IToken> tokens)
        {
            var current = tokens.Current;

            if (current.Type != TokenType.CloseGroup)
            {
                return ParseAssignment(tokens);
            }

            return new EmptyExpression(current.Start);
        }

        private IExpression ParseInitialization(IEnumerator<IToken> tokens, ref Boolean declared)
        {
            if (tokens.Current.Type == TokenType.OpenGroup)
            {
                var current = tokens.NextNonIgnorable().Current;

                if (current.Is(Keywords.Var))
                {
                    declared = true;
                    tokens.NextNonIgnorable();
                }

                var expr = ParseAssignment(tokens);
                CheckProperlyTerminated(tokens, ref expr);
                return expr;
            }

            return new InvalidExpression(ErrorCode.OpenGroupExpected, tokens.Current);
        }

        private IExpression ParseAssignment(IEnumerator<IToken> tokens)
        {
            var token = tokens.Current;

            if (token.Type != TokenType.Comma)
            {
                var x = ParseRange(tokens);
                var mode = tokens.Current.Type;

                if (mode == TokenType.Assignment)
                {
                    var y = ParseAssignment(tokens.NextNonIgnorable());
                    return new AssignmentExpression(x, y);
                }
                else if (mode == TokenType.Lambda)
                {
                    var parameters = GetParameters(x);
                    return ParseFunction(parameters, tokens.NextNonIgnorable());
                }

                return x;
            }

            return new InvalidExpression(ErrorCode.ExpressionExpected, token);
        }

        private IExpression ParseRange(IEnumerator<IToken> tokens)
        {
            var x = ParseConditional(tokens);

            if (tokens.Current.Type == TokenType.Colon)
            {
                var z = ParseConditional(tokens.NextNonIgnorable());
                var y = z;

                if (tokens.Current.Type == TokenType.Colon)
                {
                    z = ParseConditional(tokens.NextNonIgnorable());
                }
                else
                {
                    y = new EmptyExpression(z.Start);
                }

                return new RangeExpression(x, y, z);
            }

            return x;
        }

        private IExpression ParseConditional(IEnumerator<IToken> tokens)
        {
            var x = ParsePipe(tokens);

            if (tokens.Current.Type == TokenType.Condition)
            {
                var y = ParseConditional(tokens.NextNonIgnorable());
                var z = default(IExpression);

                if (tokens.Current.Type == TokenType.Colon)
                {
                    z = ParseConditional(tokens.NextNonIgnorable());
                }
                else
                {
                    z = ParseInvalid(ErrorCode.BranchMissing, tokens);
                }

                return new ConditionalExpression(x, y, z);
            }

            return x;
        }

        private IExpression ParsePipe(IEnumerator<IToken> tokens)
        {
            var x = ParseOr(tokens);

            while (tokens.Current.Type == TokenType.Pipe)
            {
                if (CheckAssigned(tokens))
                {
                    var y = ParseAssignment(tokens.NextNonIgnorable());
                    var z = new BinaryExpression.Pipe(x, y);
                    return new AssignmentExpression(x, z);
                }
                else
                {
                    var y = ParseOr(tokens);
                    x = new BinaryExpression.Pipe(x, y);
                }
            }

            return x;
        }

        private IExpression ParseOr(IEnumerator<IToken> tokens)
        {
            var x = ParseAnd(tokens);

            while (tokens.Current.Type == TokenType.Or)
            {
                var y = ParseAnd(tokens.NextNonIgnorable());
                x = new BinaryExpression.Or(x, y);
            }

            return x;
        }

        private IExpression ParseAnd(IEnumerator<IToken> tokens)
        {
            var x = ParseEquality(tokens);

            while (tokens.Current.Type == TokenType.And)
            {
                var y = ParseEquality(tokens.NextNonIgnorable());
                x = new BinaryExpression.And(x, y);
            }

            return x;
        }

        private IExpression ParseEquality(IEnumerator<IToken> tokens)
        {
            var x = ParseRelational(tokens);

            while (tokens.Current.IsEither(TokenType.Equal, TokenType.NotEqual))
            {
                var mode = tokens.Current.Type;
                var y = ParseRelational(tokens.NextNonIgnorable());
                x = ExpressionCreators.Binary[mode].Invoke(x, y);
            }

            return x;
        }

        private IExpression ParseRelational(IEnumerator<IToken> tokens)
        {
            var x = ParseAdditive(tokens);

            while (tokens.Current.IsOneOf(TokenType.Greater, TokenType.GreaterEqual, TokenType.LessEqual, TokenType.Less))
            {
                var mode = tokens.Current.Type;
                var y = ParseAdditive(tokens.NextNonIgnorable());
                x = ExpressionCreators.Binary[mode].Invoke(x, y);
            }

            return x;
        }

        private IExpression ParseAdditive(IEnumerator<IToken> tokens)
        {
            var x = ParseMultiplicative(tokens);

            while (tokens.Current.IsEither(TokenType.Add, TokenType.Subtract))
            {
                var mode = tokens.Current.Type;

                if (CheckAssigned(tokens))
                {
                    var y = ParseAssignment(tokens.NextNonIgnorable());
                    var z = ExpressionCreators.Binary[mode].Invoke(x, y);
                    return new AssignmentExpression(x, z);
                }
                else
                {
                    var y = ParseMultiplicative(tokens);
                    x = ExpressionCreators.Binary[mode].Invoke(x, y);
                }
            }

            return x;
        }

        private IExpression ParseMultiplicative(IEnumerator<IToken> tokens)
        {
            var x = ParsePower(tokens);

            while (tokens.Current.IsOneOf(TokenType.Multiply, TokenType.Modulo, TokenType.LeftDivide, TokenType.RightDivide, 
                TokenType.Number, TokenType.Identifier, TokenType.Keyword, TokenType.OpenList))
            {
                var current = tokens.Current;
                var implicitMultiply = !current.IsOneOf(TokenType.Multiply, TokenType.Modulo, TokenType.LeftDivide, TokenType.RightDivide);
                var mode = implicitMultiply ? TokenType.Multiply : current.Type;

                if (!implicitMultiply && CheckAssigned(tokens))
                {
                    var y = ParseAssignment(tokens.NextNonIgnorable());
                    var z = ExpressionCreators.Binary[mode].Invoke(x, y);
                    return new AssignmentExpression(x, z);
                }
                else
                {
                    var y = ParsePower(tokens);
                    x = ExpressionCreators.Binary[mode].Invoke(x, y);
                }
            }

            return x;
        }

        private IExpression ParsePower(IEnumerator<IToken> tokens)
        {
            var atom = ParseUnary(tokens);

            if (tokens.Current.Type == TokenType.Power)
            {
                var expressions = new Stack<IExpression>();

                if (!CheckAssigned(tokens))
                {
                    expressions.Push(atom);

                    do
                    {
                        var x = ParseUnary(tokens);
                        expressions.Push(x);
                    }
                    while (tokens.Current.Type == TokenType.Power && tokens.NextNonIgnorable() != null);

                    do
                    {
                        var right = expressions.Pop();
                        var left = expressions.Pop();
                        var x = new BinaryExpression.Power(left, right);
                        expressions.Push(x);
                    }
                    while (expressions.Count > 1);

                    atom = expressions.Pop();
                }
                else
                {
                    var rest = ParseAssignment(tokens.NextNonIgnorable());
                    var rhs = new BinaryExpression.Power(atom, rest);
                    return new AssignmentExpression(atom, rhs);
                }
            }

            return atom;
        }

        private IExpression ParseUnary(IEnumerator<IToken> tokens)
        {
            var current = tokens.Current;
            var creator = default(Func<TextPosition, IExpression, PreUnaryExpression>);

            if (ExpressionCreators.PreUnary.TryGetValue(current.Type, out creator))
            {
                var expr = ParseUnary(tokens.NextNonIgnorable());
                return creator.Invoke(current.Start, expr);
            }

            return ParsePrimary(tokens);
        }

        private IExpression ParsePrimary(IEnumerator<IToken> tokens)
        {
            var left = ParseAtomic(tokens);

            do
            {
                var current = tokens.Current;
                var mode = current.Type;
                var creator = default(Func<IExpression, TextPosition, PostUnaryExpression>);

                if (ExpressionCreators.PostUnary.TryGetValue(mode, out creator))
                {
                    tokens.NextNonIgnorable();
                    left = creator.Invoke(left, current.End);
                }
                else if (mode == TokenType.Dot)
                {
                    var identifier = ParseIdentifier(tokens.NextNonIgnorable());
                    left = new MemberExpression(left, identifier);
                }
                else if (mode == TokenType.OpenGroup)
                {
                    var arguments = ParseArguments(tokens);
                    left = new CallExpression(left, arguments);
                }
                else
                {
                    return left;
                }
            }
            while (true);
        }

        private ArgumentsExpression ParseArguments(IEnumerator<IToken> tokens)
        {
            var start = tokens.Current.Start;
            var token = tokens.NextNonIgnorable().Current;
            var arguments = default(List<IExpression>);

            if (token.Type != TokenType.CloseGroup)
            {
                arguments = ParseExpressions(tokens);
                token = tokens.Current;
            }
            else
            {
                arguments = new List<IExpression>();
            }

            var end = token.End;

            if (token.Type == TokenType.CloseGroup)
            {
                tokens.NextNonIgnorable();
            }
            else
            {
                var expr = ParseInvalid(ErrorCode.BracketNotTerminated, tokens);
                arguments.Add(expr);
            }

            return new ArgumentsExpression(arguments.ToArray(), start, end);
        }

        private MatrixExpression ParseMatrix(IEnumerator<IToken> tokens)
        {
            var start = tokens.Current;
            var values = ParseRows(tokens.NextNonIgnorable());
            var end = tokens.Current;

            if (end.Type == TokenType.CloseList)
            {
                tokens.NextNonIgnorable();
            }
            else
            {
                var error = ParseInvalid(ErrorCode.MatrixNotTerminated, tokens);
                values.Add(new IExpression[] { error });
            }

            return new MatrixExpression(values.ToArray(), start.Start, end.End);
        }

        private List<IExpression> ParseColumns(IEnumerator<IToken> tokens)
        {
            var expressions = new List<IExpression>();

            do
            {
                var expression = ParseAssignment(tokens);
                expressions.Add(expression);
            } 
            while (tokens.Current.Type == TokenType.Comma && tokens.NextNonIgnorable().Current.Type != TokenType.End);

            return expressions;
        }

        private List<IExpression[]> ParseRows(IEnumerator<IToken> tokens)
        {
            var expressions = new List<IExpression[]>();

            if (tokens.Current.IsNeither(TokenType.CloseList, TokenType.End))
            {
                var columns = ParseColumns(tokens);
                expressions.Add(columns.ToArray());

                while (tokens.Current.Type == TokenType.SemiColon)
                {
                    if (tokens.NextNonIgnorable().Current.IsNeither(TokenType.CloseList, TokenType.End))
                    {
                        columns = ParseColumns(tokens);
                        expressions.Add(columns.ToArray());
                    }
                }
            }

            return expressions;
        }

        private IExpression ParseAwait(IEnumerator<IToken> tokens)
        {
            var start = tokens.Current.Start;
            var expr = ParsePrimary(tokens.NextNonIgnorable());
            return new AwaitExpression(start, expr);
        }

        private IExpression ParseDelete(IEnumerator<IToken> tokens)
        {
            var start = tokens.Current.Start;
            var expr = ParsePrimary(tokens.NextNonIgnorable());
            return new DeleteExpression(start, expr);
        }

        private IExpression ParseObject(IEnumerator<IToken> tokens)
        {
            var start = tokens.Current;
            var current = tokens.NextNonIgnorable().Current;

            if (current.Type == TokenType.OpenScope)
            {
                var values = ParseProperties(tokens.NextNonIgnorable());
                var end = tokens.Current;

                if (end.Type == TokenType.CloseScope)
                {
                    tokens.NextNonIgnorable();
                }
                else
                {
                    var error = ParseInvalid(ErrorCode.ObjectNotTerminated, tokens);
                    values.Add(error);
                }

                return new ObjectExpression(values.ToArray(), start.Start, end.End);
            }

            return ParseInvalid(ErrorCode.BracketExpected, tokens);
        }

        private PropertyExpression ParseProperty(IEnumerator<IToken> tokens)
        {
            var identifier = ParseIdentifierOrString(tokens);
            var value = default(IExpression);

            if (tokens.Current.Type != TokenType.Colon)
            {
                value = ParseInvalid(ErrorCode.ColonExpected, tokens);
            }
            else
            {
                value = ParseAssignment(tokens.NextNonIgnorable());
            }

            return new PropertyExpression(identifier, value);
        }

        private List<IExpression> ParseProperties(IEnumerator<IToken> tokens)
        {
            var expressions = new List<IExpression>();

            if (tokens.Current.IsNeither(TokenType.CloseScope, TokenType.End))
            {
                var expression = ParseProperty(tokens);
                expressions.Add(expression);

                while (tokens.Current.Type == TokenType.Comma)
                {
                    if (tokens.NextNonIgnorable().Current.IsNeither(TokenType.CloseScope, TokenType.End))
                    {
                        expression = ParseProperty(tokens);
                        expressions.Add(expression);
                    }
                }
            }

            return expressions;
        }

        private IExpression ParseAtomic(IEnumerator<IToken> tokens)
        {
            switch (tokens.Current.Type)
            {
                case TokenType.OpenList:
                    return ParseMatrix(tokens);
                case TokenType.OpenGroup:
                    return ParseArguments(tokens);
                case TokenType.Keyword:
                    return ParseKeywordConstant(tokens);
                case TokenType.Identifier:
                    return ParseVariable(tokens);
                case TokenType.Number:
                    return ParseNumber(tokens);
                case TokenType.InterpolatedString:
                    return ParseInterpolated(tokens);
                case TokenType.String:
                    return ParseString(tokens);
                case TokenType.SemiColon:
                case TokenType.End:
                    return new EmptyExpression(tokens.Current.Start);
            }

            return ParseInvalid(ErrorCode.InvalidSymbol, tokens);
        }

        private static InvalidExpression ParseInvalid(ErrorCode code, IEnumerator<IToken> tokens)
        {
            var expr = new InvalidExpression(code, tokens.Current);
            tokens.NextNonIgnorable();
            return expr;
        }

        private IExpression ParseVariable(IEnumerator<IToken> tokens)
        {
            var token = tokens.Current;

            if (token.Type == TokenType.Identifier)
            {
                var expr = new VariableExpression(token.Payload, _scopes.Current, token.Start, token.End);
                tokens.NextNonIgnorable();
                return expr;
            }

            return ParseInvalid(ErrorCode.IdentifierExpected, tokens);
        }

        private static IExpression ParseIdentifier(IEnumerator<IToken> tokens)
        {
            var token = tokens.Current;

            if (token.Type == TokenType.Identifier)
            {
                var expr = new IdentifierExpression(token.Payload, token.Start, token.End);
                tokens.NextNonIgnorable();
                return expr;
            }

            return ParseInvalid(ErrorCode.IdentifierExpected, tokens);
        }

        private static IExpression ParseIdentifierOrString(IEnumerator<IToken> tokens)
        {
            return tokens.Current.Type == TokenType.String ? ParseString(tokens) : ParseIdentifier(tokens);
        }

        private IExpression ParseKeywordConstant(IEnumerator<IToken> tokens)
        {
            var token = tokens.Current;
            var constant = default(Object);

            if (token.Is(Keywords.New))
            {
                return ParseObject(tokens);
            }
            else if (token.Is(Keywords.Await))
            {
                return ParseAwait(tokens);
            }
            else if (token.Is(Keywords.Delete))
            {
                return ParseDelete(tokens);
            }
            else if (Keywords.TryGetConstant(token.Payload, out constant))
            {
                var expr = ConstantExpression.From(constant, token);
                tokens.NextNonIgnorable();
                return expr;
            }

            return ParseInvalid(ErrorCode.KeywordUnexpected, tokens);
        }

        private IExpression ParseInterpolated(IEnumerator<IToken> tokens)
        {
            var token = (InterpolatedToken)tokens.Current;
            var replacements = new IExpression[token.ReplacementCount];
            var format = new ConstantExpression.StringConstant(token.Payload, token, token.Errors);

            for (var i = 0; i < replacements.Length; i++)
            {
                var subtokens = token[i].GetEnumerator();
                replacements[i] = ParseExpression(subtokens);
            }

            tokens.NextNonIgnorable();
            return new InterpolatedExpression(format, replacements);
        }

        private static ConstantExpression ParseString(IEnumerator<IToken> tokens)
        {
            var token = (StringToken)tokens.Current;
            var expr = new ConstantExpression.StringConstant(token.Payload, token, token.Errors);
            tokens.NextNonIgnorable();
            return expr;
        }

        private static ConstantExpression ParseNumber(IEnumerator<IToken> tokens)
        {
            var token = (NumberToken)tokens.Current;
            var expr = new ConstantExpression.NumberConstant(token.Value, token, token.Errors);
            tokens.NextNonIgnorable();
            return expr;
        }

        #endregion

        #region Helpers

        private static ParameterExpression GetParameters(IExpression x)
        {
            var args = x as ArgumentsExpression;
            return args != null ?
                new ParameterExpression(args.Arguments, args.Start, args.End) :
                new ParameterExpression(new[] { x }, x.Start, x.End);
        }

        private static Boolean CheckAssigned(IEnumerator<IToken> tokens)
        {
            if (tokens.MoveNext())
            {
                if (tokens.Current.Type == TokenType.Assignment)
                {
                    return true;
                }
                else if (tokens.Current.IsIgnorable())
                {
                    tokens.NextNonIgnorable();
                }
            }

            return false;
        }

        private static void CheckProperlyTerminated(IEnumerator<IToken> tokens, ref IExpression expr)
        {
            if (tokens.Current.Type == TokenType.SemiColon)
            {
                tokens.NextNonIgnorable();
            }
            else
            {
                var invalid = new InvalidExpression(ErrorCode.TerminatorExpected, tokens.Current);
                expr = new BinaryExpression.Multiply(expr, invalid);
            }
        }

        private static void CheckProperStatementEnd(IEnumerator<IToken> tokens, ref IExpression expr)
        {
            if (tokens.Current.Type == TokenType.SemiColon)
            {
                tokens.NextNonIgnorable();
            }
            else if (tokens.Current.Type != TokenType.End)
            {
                var invalid = ParseInvalid(ErrorCode.InvalidSymbol, tokens);
                expr = new BinaryExpression.Multiply(expr, invalid);
            }
        }

        #endregion
    }
}
