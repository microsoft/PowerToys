// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices.ComTypes;
using Common.ComInterlop;

namespace Common
{
    /// <summary>
    /// Extends the <see cref="PreviewHandlerBase" /> by implementing IInitializeWithStream.
    /// </summary>
    public abstract class SteamBasedPreviewHandler : PreviewHandlerBase, IInitializeWithStream
    {
        /// <summary>
        /// Gets the stream object to access file.
        /// </summary>
        public IStream Stream { get; private set; }

        /// <inheritdoc/>
        public void Initialize(IStream pstream, uint grfMode)
        {
            // Ignore the grfMode always use read mode to access the file.
            this.Stream = pstream;
        }
    }
}
