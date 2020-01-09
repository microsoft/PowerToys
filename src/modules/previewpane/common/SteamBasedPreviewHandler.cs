// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices.ComTypes;
using Common.ComInterlop;

namespace Common
{
    /// <summary>
    /// Todo.
    /// </summary>
    public abstract class SteamBasedPreviewHandler : PreviewHandler, IInitializeWithStream
    {
        /// <summary>
        /// Gets todo.
        /// </summary>
        public IStream Stream { get; private set; }

        /// <inheritdoc/>
        public void Initialize(IStream pstream, uint grfMode)
        {
            this.Stream = pstream;
        }
    }
}
