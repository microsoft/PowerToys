// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;
using Common.Cominterop;

namespace Common
{
    /// <summary>
    /// Extends the <see cref="PreviewHandlerBase" /> by implementing IInitializeWithFile.
    /// </summary>
    public abstract class FileBasedPreviewHandler : PreviewHandlerBase, IInitializeWithFile
    {
        /// <summary>
        /// Gets the file path.
        /// </summary>
        public string FilePath { get; private set; }

        /// <inheritdoc />
        public void Initialize([MarshalAs(UnmanagedType.LPWStr)] string pszFilePath, uint grfMode)
        {
            // Ignore the grfMode always use read mode to access the file.
            this.FilePath = pszFilePath;
        }
    }
}
