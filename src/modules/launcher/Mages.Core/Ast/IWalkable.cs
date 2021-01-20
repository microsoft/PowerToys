namespace Mages.Core.Ast
{
    /// <summary>
    /// Represents a part of the AST that can be walked.
    /// </summary>
    public interface IWalkable
    {
        /// <summary>
        /// Accepts the visitor by showing him around.
        /// </summary>
        /// <param name="visitor">The visitor walking the tree.</param>
        void Accept(ITreeWalker visitor);
    }
}
