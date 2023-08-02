// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.UI.Xaml.Media;
using Peek.FilePreviewer.Models;

namespace Peek.FilePreviewer.Previewers
{
    public interface IUnsupportedFilePreviewer : IPreviewer
    {
        public UnsupportedFilePreviewData? Preview { get; }
    }
}
