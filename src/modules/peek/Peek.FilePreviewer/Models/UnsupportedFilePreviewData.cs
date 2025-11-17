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
        private ImageSource? iconPreview;

        [ObservableProperty]
        private string? fileName;

        [ObservableProperty]
        private string? fileType;

        [ObservableProperty]
        private string? fileSize;

        [ObservableProperty]
        private string? dateModified;
    }
}
