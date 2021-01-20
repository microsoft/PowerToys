namespace Mages.Core.Ast.Statements
{
    using System.Collections.Generic;

    /// <summary>
    /// Represents a block of statements.
    /// </summary>
    public sealed class BlockStatement : BaseStatement, IStatement
    {
        #region Fields

        private readonly IStatement[] _statements;

        #endregion

        #region ctor

        /// <summary>
        /// Creates a new block statement.
        /// </summary>
        public BlockStatement(IStatement[] statements, TextPosition start, TextPosition end)
            : base(start, end)
        {
            _statements = statements;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets the contained statements.
        /// </summary>
        public IEnumerable<IStatement> Statements
        {
            get { return _statements; }
        }

        #endregion

        #region Methods

        /// <summary>
        /// Validates the expression with the given context.
        /// </summary>
        /// <param name="context">The validator to report errors to.</param>
        public void Validate(IValidationContext context)
        {
        }

        /// <summary>
        /// Accepts the visitor by showing him around.
        /// </summary>
        /// <param name="visitor">The visitor walking the tree.</param>
        public void Accept(ITreeWalker visitor)
        {
            visitor.Visit(this);
        }

        #endregion
    }
}
