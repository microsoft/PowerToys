// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;
using Common.Cominterop;

namespace Common
{
    /// <summary>
    /// Todo.
    /// </summary>
    public abstract class FileBasedPreviewHandler : PreviewHandler, IInitializeWithFile
    {
        /// <summary>
        /// Gets or file path.
        /// </summary>
        public string FilePath { get; private set; }

        /// <inheritdoc />
        public void Initialize([MarshalAs(UnmanagedType.LPWStr)] string pszFilePath, uint grfMode)
        {
            this.FilePath = pszFilePath;
        }
    }
}
