namespace Mages.Core.Tests
{
    using Mages.Core.Ast;
    using System;

    static class Helpers
    {
        public static IExpression ToExpression(this String sourceCode)
        {
            var parser = new ExpressionParser();
            return parser.ParseExpression(sourceCode);
        }

        public static IStatement ToStatement(this String sourceCode)
        {
            var parser = new ExpressionParser();
            return parser.ParseStatement(sourceCode);
        }

        public static Object Eval(this String sourceCode)
        {
            var engine = new Engine();
            return engine.Interpret(sourceCode);
        }
    }
}
