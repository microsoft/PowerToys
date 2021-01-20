namespace Mages.Core.Ast.Expressions
{
    using System;

    /// <summary>
    /// Represents an interpolated string expression.
    /// </summary>
    public sealed class InterpolatedExpression : ComputingExpression, IExpression
    {
        #region Fields

        private readonly ConstantExpression _format;
        private readonly IExpression[] _replacements;

        #endregion

        #region ctor

        /// <summary>
        /// Creates a new interpolated string expression.
        /// </summary>
        public InterpolatedExpression(ConstantExpression format, IExpression[] replacements)
            : base(format.Start, format.End)
        {
            _format = format;
            _replacements = replacements;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets the formatting string.
        /// </summary>
        public ConstantExpression Format
        {
            get { return _format; }
        }

        /// <summary>
        /// Gets the associated replacements.
        /// </summary>
        public IExpression[] Replacements
        {
            get { return _replacements; }
        }

        #endregion

        #region Methods

        /// <summary>
        /// Accepts the visitor by showing him around.
        /// </summary>
        /// <param name="visitor">The visitor walking the tree.</param>
        public void Accept(ITreeWalker visitor)
        {
            visitor.Visit(this);
        }

        /// <summary>
        /// Validates the expression with the given context.
        /// </summary>
        /// <param name="context">The validator to report errors to.</param>
        public void Validate(IValidationContext context)
        {
        }

        #endregion
    }
}
