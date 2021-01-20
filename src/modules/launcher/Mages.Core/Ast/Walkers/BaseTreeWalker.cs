namespace Mages.Core.Ast.Walkers
{
    using Mages.Core.Ast.Expressions;
    using Mages.Core.Ast.Statements;

    /// <summary>
    /// A basic tree walker to focus on what's really important.
    /// </summary>
    public abstract class BaseTreeWalker : ITreeWalker
    {
        /// <summary>
        /// Visits a var statement - accepts the assignment.
        /// </summary>
        public virtual void Visit(VarStatement statement)
        {
            statement.Assignment.Accept(this);
        }

        /// <summary>
        /// Visits a block statement - accepts all childs.
        /// </summary>
        public virtual void Visit(BlockStatement block)
        {
            foreach (var statement in block.Statements)
            {
                statement.Accept(this);
            }
        }

        /// <summary>
        /// Visits a simple statement - accepts the expression.
        /// </summary>
        public virtual void Visit(SimpleStatement statement)
        {
            statement.Expression.Accept(this);
        }

        /// <summary>
        /// Visits a return statement - accepts the payload.
        /// </summary>
        public virtual void Visit(ReturnStatement statement)
        {
            statement.Expression.Accept(this);
        }

        /// <summary>
        /// Visits a delete expression - accepts the payload.
        /// </summary>
        public virtual void Visit(DeleteExpression expression)
        {
            expression.Payload.Accept(this);
        }

        /// <summary>
        /// Visits a while statement - accepts the condition and body.
        /// </summary>
        public virtual void Visit(WhileStatement statement)
        {
            statement.Condition.Accept(this);
            statement.Body.Accept(this);
        }

        /// <summary>
        /// Visits a for statement - accepts the initialization, condition, afterthought, and body.
        /// </summary>
        public virtual void Visit(ForStatement statement)
        {
            statement.Initialization.Accept(this);
            statement.Condition.Accept(this);
            statement.Body.Accept(this);
            statement.AfterThought.Accept(this);
        }

        /// <summary>
        /// Visits an if statement - accepts the condition and body.
        /// </summary>
        public virtual void Visit(IfStatement statement)
        {
            statement.Condition.Accept(this);
            statement.Primary.Accept(this);
            statement.Secondary.Accept(this);
        }

        /// <summary>
        /// Visits a match statement - accepts the condition and body.
        /// </summary>
        public virtual void Visit(MatchStatement statement)
        {
            statement.Reference.Accept(this);
            statement.Cases.Accept(this);
        }

        /// <summary>
        /// Visits a case statement - accepts the condition and body.
        /// </summary>
        public virtual void Visit(CaseStatement statement)
        {
            statement.Condition.Accept(this);
            statement.Body.Accept(this);
        }

        /// <summary>
        /// Visits a continue statement.
        /// </summary>
        public virtual void Visit(ContinueStatement statement)
        {
        }

        /// <summary>
        /// Visits a break statement.
        /// </summary>
        public virtual void Visit(BreakStatement statement)
        {
        }

        /// <summary>
        /// Visits an empty expression.
        /// </summary>
        public virtual void Visit(EmptyExpression expression)
        {
        }

        /// <summary>
        /// Visits a constant expression.
        /// </summary>
        public virtual void Visit(ConstantExpression expression)
        {
        }

        /// <summary>
        /// Visits an awaitable expression.
        /// </summary>
        public virtual void Visit(AwaitExpression expression)
        {
            expression.Payload.Accept(this);
        }

        /// <summary>
        /// Visits an arguments expression - accepts all arguments.
        /// </summary>
        public virtual void Visit(ArgumentsExpression expression)
        {
            foreach (var argument in expression.Arguments)
            {
                argument.Accept(this);
            }
        }

        /// <summary>
        /// Visits an assignment expression - accepts the variable and value.
        /// </summary>
        public virtual void Visit(AssignmentExpression expression)
        {
            expression.Variable.Accept(this);
            expression.Value.Accept(this);
        }

        /// <summary>
        /// Visits a binary expression - accepts the left and right value.
        /// </summary>
        public virtual void Visit(BinaryExpression expression)
        {
            expression.LValue.Accept(this);
            expression.RValue.Accept(this);
        }

        /// <summary>
        /// Visits an interpolated string - accepts the format and replacements.
        /// </summary>
        public virtual void Visit(InterpolatedExpression expression)
        {
            expression.Format.Accept(this);

            foreach (var replacement in expression.Replacements)
            {
                replacement.Accept(this);
            }
        }

        /// <summary>
        /// Visits a pre-unary expression - accepts the value.
        /// </summary>
        public virtual void Visit(PreUnaryExpression expression)
        {
            expression.Value.Accept(this);
        }

        /// <summary>
        /// Visits a post-unary expression - accepts the value.
        /// </summary>
        public virtual void Visit(PostUnaryExpression expression)
        {
            expression.Value.Accept(this);
        }

        /// <summary>
        /// Visits a range expression - accepts the from, step. and to.
        /// </summary>
        public virtual void Visit(RangeExpression expression)
        {
            expression.From.Accept(this);
            expression.Step.Accept(this);
            expression.To.Accept(this);
        }

        /// <summary>
        /// Visits a conditional expression - accepts the condition, primary, and secondary.
        /// </summary>
        public virtual void Visit(ConditionalExpression expression)
        {
            expression.Condition.Accept(this);
            expression.Primary.Accept(this);
            expression.Secondary.Accept(this);
        }

        /// <summary>
        /// Visits a call expression - accepts the function and arguments.
        /// </summary>
        public virtual void Visit(CallExpression expression)
        {
            expression.Function.Accept(this);
            expression.Arguments.Accept(this);
        }

        /// <summary>
        /// Visits an object expression - accepts all values.
        /// </summary>
        public virtual void Visit(ObjectExpression expression)
        {
            foreach (var value in expression.Values)
            {
                value.Accept(this);
            }
        }

        /// <summary>
        /// Visits a property expression - accepts the name and value.
        /// </summary>
        public virtual void Visit(PropertyExpression expression)
        {
            expression.Name.Accept(this);
            expression.Value.Accept(this);
        }

        /// <summary>
        /// Visits a matrix expression - accepts all values.
        /// </summary>
        public virtual void Visit(MatrixExpression expression)
        {
            foreach (var row in expression.Values)
            {
                foreach (var value in row)
                {
                    value.Accept(this);
                }
            }
        }

        /// <summary>
        /// Visits a function expression - accepts the parameters and body.
        /// </summary>
        public virtual void Visit(FunctionExpression expression)
        {
            expression.Parameters.Accept(this);
            expression.Body.Accept(this);
        }

        /// <summary>
        /// Visits an invalid expression.
        /// </summary>
        public virtual void Visit(InvalidExpression expression)
        {
        }

        /// <summary>
        /// Visits an identifier expression.
        /// </summary>
        public virtual void Visit(IdentifierExpression expression)
        {
        }

        /// <summary>
        /// Visits a member expression - accepts the object and member.
        /// </summary>
        public virtual void Visit(MemberExpression expression)
        {
            expression.Object.Accept(this);
            expression.Member.Accept(this);
        }

        /// <summary>
        /// Visits a parameter expression - accepts all parameters.
        /// </summary>
        public virtual void Visit(ParameterExpression expression)
        {
            foreach (var parameter in expression.Parameters)
            {
                parameter.Accept(this);
            }
        }

        /// <summary>
        /// Visits a variable expression.
        /// </summary>
        public virtual void Visit(VariableExpression expression)
        {
        }
    }
}
