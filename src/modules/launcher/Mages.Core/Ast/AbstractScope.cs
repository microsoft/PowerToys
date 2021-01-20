namespace Mages.Core.Ast
{
    /// <summary>
    /// Represents an abstract (compile-time) scope information.
    /// </summary>
    public sealed class AbstractScope
    {
        #region Fields

        private readonly AbstractScope _parent;

        #endregion

        #region ctor

        /// <summary>
        /// Creates a new abstract scope.
        /// </summary>
        /// <param name="parent">The parent scope to use, if any.</param>
        public AbstractScope(AbstractScope parent)
        {
            _parent = parent;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets the parent scope.
        /// </summary>
        public AbstractScope Parent
        {
            get { return _parent; }
        }

        #endregion
    }
}
