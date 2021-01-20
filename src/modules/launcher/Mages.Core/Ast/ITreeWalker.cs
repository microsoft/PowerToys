namespace Mages.Core.Ast
{
    using Mages.Core.Ast.Expressions;
    using Mages.Core.Ast.Statements;

    /// <summary>
    /// Represents a syntax tree walker.
    /// </summary>
    public interface ITreeWalker
    {
        /// <summary>
        /// Visits the given statement.
        /// </summary>
        /// <param name="statement">Variable statement.</param>
        void Visit(VarStatement statement);

        /// <summary>
        /// Visits the given statement.
        /// </summary>
        /// <param name="statement">Block statement.</param>
        void Visit(BlockStatement statement);

        /// <summary>
        /// Visits the given statement.
        /// </summary>
        /// <param name="statement">Simple statement.</param>
        void Visit(SimpleStatement statement);

        /// <summary>
        /// Visits the given statement.
        /// </summary>
        /// <param name="statement">Return statement.</param>
        void Visit(ReturnStatement statement);

        /// <summary>
        /// Visits the given statement.
        /// </summary>
        /// <param name="statement">While statement.</param>
        void Visit(WhileStatement statement);

        /// <summary>
        /// Visits the given statement.
        /// </summary>
        /// <param name="statement">For statement.</param>
        void Visit(ForStatement statement);

        /// <summary>
        /// Visits the given statement.
        /// </summary>
        /// <param name="statement">If statement.</param>
        void Visit(IfStatement statement);

        /// <summary>
        /// Visits the given statement.
        /// </summary>
        /// <param name="statement">Match statement.</param>
        void Visit(MatchStatement statement);

        /// <summary>
        /// Visits the given statement.
        /// </summary>
        /// <param name="statement">Case statement.</param>
        void Visit(CaseStatement statement);

        /// <summary>
        /// Visits the given statement.
        /// </summary>
        /// <param name="statement">Continue statement.</param>
        void Visit(ContinueStatement statement);

        /// <summary>
        /// Visits the given statement.
        /// </summary>
        /// <param name="statement">Break statement.</param>
        void Visit(BreakStatement statement);

        /// <summary>
        /// Visits the given expression.
        /// </summary>
        /// <param name="expression">Empty expression.</param>
        void Visit(EmptyExpression expression);

        /// <summary>
        /// Visits the given expression.
        /// </summary>
        /// <param name="expression">Constant expression.</param>
        void Visit(ConstantExpression expression);

        /// <summary>
        /// Visits the given expression.
        /// </summary>
        /// <param name="expression">Interpolated expression.</param>
        void Visit(InterpolatedExpression expression);

        /// <summary>
        /// Visits the given expression.
        /// </summary>
        /// <param name="expression">Arguments expression.</param>
        void Visit(ArgumentsExpression expression);

        /// <summary>
        /// Visits the given expression.
        /// </summary>
        /// <param name="expression">Assignment expression.</param>
        void Visit(AssignmentExpression expression);

        /// <summary>
        /// Visits the given expression.
        /// </summary>
        /// <param name="expression">Binary expression.</param>
        void Visit(BinaryExpression expression);

        /// <summary>
        /// Visits the given expression.
        /// </summary>
        /// <param name="expression">Pre-unary expression.</param>
        void Visit(PreUnaryExpression expression);

        /// <summary>
        /// Visits the given expression.
        /// </summary>
        /// <param name="expression">Post-unary expression.</param>
        void Visit(PostUnaryExpression expression);

        /// <summary>
        /// Visits the given expression.
        /// </summary>
        /// <param name="expression">Range expression.</param>
        void Visit(RangeExpression expression);

        /// <summary>
        /// Visits the given expression.
        /// </summary>
        /// <param name="expression">Conditional expression.</param>
        void Visit(ConditionalExpression expression);

        /// <summary>
        /// Visits the given expression.
        /// </summary>
        /// <param name="expression">Call expression.</param>
        void Visit(CallExpression expression);

        /// <summary>
        /// Visits the given expression.
        /// </summary>
        /// <param name="expression">Object expression.</param>
        void Visit(ObjectExpression expression);

        /// <summary>
        /// Visits the given expression.
        /// </summary>
        /// <param name="expression">Property expression.</param>
        void Visit(PropertyExpression expression);

        /// <summary>
        /// Visits the given expression.
        /// </summary>
        /// <param name="expression">Matrix expression.</param>
        void Visit(MatrixExpression expression);

        /// <summary>
        /// Visits the given expression.
        /// </summary>
        /// <param name="expression">Function expression.</param>
        void Visit(FunctionExpression expression);

        /// <summary>
        /// Visits the given expression.
        /// </summary>
        /// <param name="expression">Invalid expression.</param>
        void Visit(InvalidExpression expression);

        /// <summary>
        /// Visits the given expression.
        /// </summary>
        /// <param name="expression">Identifier expression.</param>
        void Visit(IdentifierExpression expression);

        /// <summary>
        /// Visits the given expression.
        /// </summary>
        /// <param name="expression">Member expression.</param>
        void Visit(MemberExpression expression);

        /// <summary>
        /// Visits the given expression.
        /// </summary>
        /// <param name="expression">Parameter expression.</param>
        void Visit(ParameterExpression expression);

        /// <summary>
        /// Visits the given expression.
        /// </summary>
        /// <param name="expression">Variable expression.</param>
        void Visit(VariableExpression expression);

        /// <summary>
        /// Visits the given expression.
        /// </summary>
        /// <param name="expression">Delete expression.</param>
        void Visit(DeleteExpression expression);

        /// <summary>
        /// Visits the given experssion.
        /// </summary>
        /// <param name="expression">Await expression.</param>
        void Visit(AwaitExpression expression);
    }
}
