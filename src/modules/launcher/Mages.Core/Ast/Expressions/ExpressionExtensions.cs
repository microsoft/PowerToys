namespace Mages.Core.Ast.Expressions
{
    static class ExpressionExtensions
    {
        public static TextPosition GetStart(this IExpression[] expressions)
        {
            var length = expressions.Length;
            return length > 0 ? expressions[0].Start : default(TextPosition);
        }

        public static TextPosition GetEnd(this IExpression[] expressions)
        {
            var length = expressions.Length;
            return length > 0 ? expressions[length - 1].End : default(TextPosition);
        }

        public static void Validate(this IExpression[] expressions, IValidationContext context)
        {
            foreach (var expression in expressions)
            {
                expression.Validate(context);
            }
        }
    }
}
