// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq.Expressions;

namespace HostsUILib.Helpers
{
    // https://stackoverflow.com/a/22569086
    public static class ExpressionExtensions
    {
        public static Expression<Func<T, bool>> And<T>(
            this Expression<Func<T, bool>> expr1,
            Expression<Func<T, bool>> expr2)
        {
            var secondBody = expr2.Body.Replace(expr2.Parameters[0], expr1.Parameters[0]);
            return Expression.Lambda<Func<T, bool>>(Expression.AndAlso(expr1.Body, secondBody), expr1.Parameters);
        }

        public static Expression Replace(this Expression expression, Expression searchEx, Expression replaceEx)
        {
            return new ReplaceVisitor(searchEx, replaceEx).Visit(expression);
        }

        internal sealed class ReplaceVisitor : ExpressionVisitor
        {
            private readonly Expression _from;
            private readonly Expression _to;

            public ReplaceVisitor(Expression from, Expression to)
            {
                _from = from;
                _to = to;
            }

            public override Expression Visit(Expression node)
            {
                return node == _from ? _to : base.Visit(node);
            }
        }
    }
}
