// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Windows.Win32.UI.Shell;

namespace Peek.FilePreviewer.Previewers
{
    public interface IShellPreviewHandlerPreviewer : IPreviewer
    {
        public IPreviewHandler? Preview { get; }

        public void Clear();
    }
}
