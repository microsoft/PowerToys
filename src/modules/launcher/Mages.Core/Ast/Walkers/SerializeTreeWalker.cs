namespace Mages.Core.Ast.Walkers
{
    using Mages.Core.Ast.Expressions;
    using Mages.Core.Ast.Statements;
    using Mages.Core.Runtime;
    using System;
    using System.IO;

    /// <summary>
    /// Represents the walker to serialize the AST.
    /// </summary>
    public sealed class SerializeTreeWalker : ITreeWalker
    {
        #region Fields

        private readonly TextWriter _writer;
        private Int32 _level;

        #endregion

        #region ctor

        /// <summary>
        /// Creates a new serialization tree walker.
        /// </summary>
        /// <param name="writer">The destination to write to.</param>
        public SerializeTreeWalker(TextWriter writer)
        {
            _writer = writer;
            _level = 0;
            _writer.Write("Root: ");
        }

        #endregion

        #region Tree Walker

        void ITreeWalker.Visit(ArgumentsExpression expression)
        {
            Header("Expression/Arguments");

            foreach (var expr in expression.Arguments)
            {
                WriteItem(expr);
            }
        }

        void ITreeWalker.Visit(BlockStatement block)
        {
            Header("Statement/Block");

            foreach (var stmt in block.Statements)
            {
                WriteItem(stmt);
            }
        }

        void ITreeWalker.Visit(AssignmentExpression expression)
        {
            Header("Expression/Assignment");
            WriteProperty("Variable", expression.Variable);
            WriteProperty("Value", expression.Value);
        }

        void ITreeWalker.Visit(BinaryExpression expression)
        {
            Header("Expression/Binary/" + expression.Operator);
            WriteProperty("LValue", expression.LValue);
            WriteProperty("RValue", expression.RValue);
        }

        void ITreeWalker.Visit(BreakStatement statement)
        {
            Header("Statement/Break");
        }

        void ITreeWalker.Visit(ContinueStatement statement)
        {
            Header("Statement/Continue");
        }

        void ITreeWalker.Visit(VarStatement statement)
        {
            Header("Statement/Variable");
            WriteProperty("Assignment", statement.Assignment);
        }

        void ITreeWalker.Visit(SimpleStatement statement)
        {
            statement.Expression.Accept(this);
        }

        void ITreeWalker.Visit(ReturnStatement statement)
        {
            Header("Statement/Return");
            WriteProperty("Payload", statement.Expression);
        }

        void ITreeWalker.Visit(WhileStatement statement)
        {
            Header("Statement/While");
            WriteProperty("Condition", statement.Condition);
            WriteProperty("Body", statement.Body);
        }

        void ITreeWalker.Visit(ForStatement statement)
        {
            Header("Statement/For");
            WriteProperty("Initialization", statement.Initialization);
            WriteProperty("Condition", statement.Condition);
            WriteProperty("AfterThought", statement.AfterThought);
            WriteProperty("Body", statement.Body);
        }

        void ITreeWalker.Visit(IfStatement statement)
        {
            Header("Statement/If");
            WriteProperty("Condition", statement.Condition);
            WriteProperty("Primary", statement.Primary);
            WriteProperty("Secondary", statement.Secondary);
        }

        void ITreeWalker.Visit(MatchStatement statement)
        {
            Header("Statement/Match");
            WriteProperty("Reference", statement.Reference);
            WriteProperty("Cases", statement.Cases);
        }

        void ITreeWalker.Visit(CaseStatement statement)
        {
            Header("Statement/Case");
            WriteProperty("Condition", statement.Condition);
            WriteProperty("Body", statement.Body);
        }

        void ITreeWalker.Visit(EmptyExpression expression)
        {
            Header("Expression/Empty");
        }

        void ITreeWalker.Visit(ConstantExpression expression)
        {
            Header("Expression/Constant/" + expression.Value.GetType().Name);
            WriteLine("- Value: " + Stringify.This(expression.Value));
        }

        void ITreeWalker.Visit(InterpolatedExpression expression)
        {
            Header("Expression/InterpolatedString");
            WriteProperty("Format", expression.Format);
            WriteLine("- Replacements:");
            _level++;

            foreach (var replacement in expression.Replacements)
            {
                WriteItem(replacement);
            }

            _level--;
        }

        void ITreeWalker.Visit(PreUnaryExpression expression)
        {
            Header("Expression/PreUnary/" + expression.Operator);
            WriteProperty("Value", expression.Value);
        }

        void ITreeWalker.Visit(PostUnaryExpression expression)
        {
            Header("Expression/PostUnary/" + expression.Operator);
            WriteProperty("Value", expression.Value);
        }

        void ITreeWalker.Visit(RangeExpression expression)
        {
            Header("Expression/Range");
            WriteProperty("From", expression.From);
            WriteProperty("Step", expression.Step);
            WriteProperty("To", expression.To);
        }

        void ITreeWalker.Visit(ConditionalExpression expression)
        {
            Header("Expression/Condition");
            WriteProperty("Condition", expression.Condition);
            WriteProperty("Primary", expression.Primary);
            WriteProperty("Secondary", expression.Secondary);
        }

        void ITreeWalker.Visit(CallExpression expression)
        {
            Header("Expression/Call");
            WriteProperty("Function", expression.Function);
            WriteProperty("Arguments", expression.Arguments);
        }

        void ITreeWalker.Visit(ObjectExpression expression)
        {
            Header("Expression/Object");

            foreach (var value in expression.Values)
            {
                WriteItem(value);
            }
        }

        void ITreeWalker.Visit(PropertyExpression expression)
        {
            Header("Expression/Property");
            WriteProperty("Name", expression.Name);
            WriteProperty("Value", expression.Value);
        }

        void ITreeWalker.Visit(MatrixExpression expression)
        {
            Header("Expression/Matrix");
            WriteLine("- Rows:");
            _level++;

            foreach (var row in expression.Values)
            {
                WriteLine("- Values:");
                _level++;

                foreach (var value in row)
                {
                    WriteItem(value);
                }

                _level--;
            }

            _level--;
        }

        void ITreeWalker.Visit(FunctionExpression expression)
        {
            Header("Expression/Function");
            WriteProperty("Parameters", expression.Parameters);
            WriteProperty("Body", expression.Body);
        }

        void ITreeWalker.Visit(DeleteExpression expression)
        {
            Header("Expression/Delete");
            WriteProperty("Payload", expression.Payload);
        }

        void ITreeWalker.Visit(AwaitExpression expression)
        {
            Header("Expression/Await");
            WriteProperty("Payload", expression.Payload);
        }

        void ITreeWalker.Visit(InvalidExpression expression)
        {
            Header("Expression/Invalid");
        }

        void ITreeWalker.Visit(IdentifierExpression expression)
        {
            Header("Expression/Identifier/" + expression.Name);
        }

        void ITreeWalker.Visit(MemberExpression expression)
        {
            Header("Expression/Member");
            WriteProperty("Object", expression.Object);
            WriteProperty("Member", expression.Member);
        }

        void ITreeWalker.Visit(ParameterExpression expression)
        {
            Header("Expression/Parameters");

            foreach (var expr in expression.Parameters)
            {
                WriteItem(expr);
            }
        }

        void ITreeWalker.Visit(VariableExpression expression)
        {
            Header("Expression/Variable/" + expression.Name);
        }

        #endregion

        #region Helpers

        private void Header(String value)
        {
            _writer.WriteLine(value);
        }

        private void WriteLine(String value)
        {
            Intent();
            _writer.WriteLine(value);
        }

        private void WriteItem(IWalkable item)
        {
            Intent();
            _writer.Write("- ");
            _level++;
            item.Accept(this);
            _level--;
        }

        private void WriteProperty(String name, IWalkable item)
        {
            Intent();
            _writer.Write("- ");
            _writer.Write(name);
            _writer.Write(": ");
            _level++;
            item.Accept(this);
            _level--;
        }

        private void Intent()
        {
            var spaces = _level * 2;

            for (var i = 0; i < spaces; i++)
            {
                _writer.Write(' ');
            }
        }

        #endregion
    }
}
