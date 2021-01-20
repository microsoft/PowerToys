namespace Mages.Core
{
    using System;

    /// <summary>
    /// Determines the placement of objects in the global scope.
    /// </summary>
    public interface IPlacement
    {
        /// <summary>
        /// Placed with the given name.
        /// </summary>
        /// <param name="name">The name to use.</param>
        void WithName(String name);

        /// <summary>
        /// Placed with the default name.
        /// </summary>
        void WithDefaultName();

        /// <summary>
        /// The children of the object are placed in the scope.
        /// </summary>
        void Scattered();
    }
}
