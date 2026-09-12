// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Peek.Common.Models;

namespace Peek.FilePreviewer.Previewers.Interfaces
{
    /// <summary>
    /// Represents a previewer that can be rebound to a new file system item without disposal and
    /// re-creation.
    /// </summary>
    public interface IReusablePreviewer
    {
        /// <summary>
        /// Rebinds the previewer instance to a new item and the current scaling context.
        /// </summary>
        /// <param name="item">The new file system item to preview.</param>
        /// <param name="scalingFactor">The current scaling factor from the host view.</param>
        void Rebind(IFileSystemItem item, double scalingFactor);
    }
}
