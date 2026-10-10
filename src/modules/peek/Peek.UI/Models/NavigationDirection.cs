// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Peek.UI.Models
{
    /// <summary>
    /// Identifies item traversal order for both paced navigation and navigation after
    /// deletion.
    /// </summary>
    internal enum NavigationDirection
    {
        /// <summary>
        /// Advance to the next available item.
        /// </summary>
        Forwards,

        /// <summary>
        /// Return to the previous available item.
        /// </summary>
        Backwards,
    }
}
