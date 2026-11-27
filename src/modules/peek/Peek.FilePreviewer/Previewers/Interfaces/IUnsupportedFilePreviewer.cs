// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Windows.Input;

using Microsoft.UI.Xaml.Media;
using Peek.FilePreviewer.Models;

namespace Peek.FilePreviewer.Previewers
{
    public interface IUnsupportedFilePreviewer : IPreviewer
    {
        public UnsupportedFilePreviewData? Preview { get; }

        /// <summary>
        /// Gets or sets the command that previews the target of the shortcut that is being shown.
        /// </summary>
        public ICommand? PeekShortcutTargetCommand { get; set; }
    }
}
