// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml.Media;

namespace Peek.FilePreviewer.Models
{
    public partial class UnsupportedFilePreviewData : ObservableObject
    {
        [ObservableProperty]
        public partial ImageSource? IconPreview { get; set; }

        [ObservableProperty]
        public partial string? FileName { get; set; }

        [ObservableProperty]
        public partial string? FileType { get; set; }

        [ObservableProperty]
        public partial string? FileSize { get; set; }

        [ObservableProperty]
        public partial string? DateModified { get; set; }
    }
}
