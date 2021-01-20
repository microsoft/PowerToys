namespace Mages.Core.Ast.Statements
{
    using System;

    /// <summary>
    /// Represents a for statement.
    /// </summary>
    public sealed class ForStatement : BreakableStatement, IStatement
    {
        #region Fields

        private readonly Boolean _declared;
        private readonly IExpression _initialization;
        private readonly IExpression _condition;
        private readonly IExpression _afterthought;

        #endregion

        #region ctor

        /// <summary>
        /// Creates a new for statement.
        /// </summary>
        public ForStatement(Boolean declared, IExpression initialization, IExpression condition, IExpression afterthought, IStatement body, TextPosition start)
            : base(body, start, body.End)
        {
            _declared = declared;
            _initialization = initialization;
            _condition = condition;
            _afterthought = afterthought;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets if the initialization variable is declared.
        /// </summary>
        public Boolean IsDeclared
        {
            get { return _declared; }
        }

        /// <summary>
        /// Gets the stored initialization.
        /// </summary>
        public IExpression Initialization
        {
            get { return _initialization; }
        }

        /// <summary>
        /// Gets the stored condition.
        /// </summary>
        public IExpression Condition
        {
            get { return _condition; }
        }

        /// <summary>
        /// Gets the stored after thought.
        /// </summary>
        public IExpression AfterThought
        {
            get { return _afterthought; }
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

        #endregion
    }
}
