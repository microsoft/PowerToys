namespace Mages.Core.Ast
{
    using Mages.Core.Ast.Expressions;
    using Mages.Core.Tokens;
    using System;
    using System.Collections.Generic;

    static class ExpressionCreators
    {
        public static readonly Dictionary<TokenType, Func<IExpression, IExpression, BinaryExpression>> Binary = new Dictionary<TokenType, Func<IExpression, IExpression, BinaryExpression>>
        {
            { TokenType.Equal, (a, b) => new BinaryExpression.Equal(a, b) },
            { TokenType.NotEqual, (a, b) => new BinaryExpression.NotEqual(a, b) },
            { TokenType.Greater, (a, b) => new BinaryExpression.Greater(a, b) },
            { TokenType.GreaterEqual, (a, b) => new BinaryExpression.GreaterEqual(a, b) },
            { TokenType.Less, (a, b) => new BinaryExpression.Less(a, b) },
            { TokenType.LessEqual, (a, b) => new BinaryExpression.LessEqual(a, b) },
            { TokenType.Add, (a, b) => new BinaryExpression.Add(a, b) },
            { TokenType.Subtract, (a, b) => new BinaryExpression.Subtract(a, b) },
            { TokenType.Multiply, (a, b) => new BinaryExpression.Multiply(a, b) },
            { TokenType.Modulo, (a, b) => new BinaryExpression.Modulo(a, b) },
            { TokenType.LeftDivide, (a, b) => new BinaryExpression.LeftDivide(a, b) },
            { TokenType.RightDivide, (a, b) => new BinaryExpression.RightDivide(a, b) },
            { TokenType.Pipe, (a, b) => new BinaryExpression.Pipe(a, b) },
        };

        public static readonly Dictionary<TokenType, Func<TextPosition, IExpression, PreUnaryExpression>> PreUnary = new Dictionary<TokenType, Func<TextPosition, IExpression, PreUnaryExpression>>
        {
            { TokenType.Add, (p, x) => new PreUnaryExpression.Plus(p, x) },
            { TokenType.Subtract, (p, x) => new PreUnaryExpression.Minus(p, x) },
            { TokenType.Negate, (p, x) => new PreUnaryExpression.Not(p, x) },
            { TokenType.Increment, (p, x) => new PreUnaryExpression.Increment(p, x) },
            { TokenType.Decrement, (p, x) => new PreUnaryExpression.Decrement(p, x) },
            { TokenType.Type, (p, x) => new PreUnaryExpression.Type(p, x) },
        };

        public static readonly Dictionary<TokenType, Func<IExpression, TextPosition, PostUnaryExpression>> PostUnary = new Dictionary<TokenType, Func<IExpression, TextPosition, PostUnaryExpression>>
        {
            { TokenType.Factorial, (x, p) => new PostUnaryExpression.Factorial(x, p) },
            { TokenType.Transpose, (x, p) => new PostUnaryExpression.Transpose(x, p) },
            { TokenType.Increment, (x, p) => new PostUnaryExpression.Increment(x, p) },
            { TokenType.Decrement, (x, p) => new PostUnaryExpression.Decrement(x, p) },
        };
    }
}
