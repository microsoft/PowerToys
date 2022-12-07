// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Peek.FilePreviewer.Previewers
{
    using Microsoft.UI.Xaml.Media.Imaging;

    public interface IUnsupportedFilePreviewer : IPreviewer
    {
        public BitmapSource? IconPreview { get; }

        public string? FileName { get; }

        public string? FileType { get; }

        public string? FileSize { get; }

        public string? DateModified { get; }
    }
}
