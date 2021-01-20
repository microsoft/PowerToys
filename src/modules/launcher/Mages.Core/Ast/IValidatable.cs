namespace Mages.Core.Ast
{
    /// <summary>
    /// Represents a part of the AST that can be validated.
    /// </summary>
    public interface IValidatable : ITextRange
    {
        /// <summary>
        /// Validates the expression with the given context.
        /// </summary>
        /// <param name="context">The validator to report errors to.</param>
        void Validate(IValidationContext context);
    }
}
