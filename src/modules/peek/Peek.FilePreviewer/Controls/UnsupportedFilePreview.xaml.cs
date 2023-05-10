// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Peek.Common.Helpers;

namespace Peek.FilePreviewer.Controls
{
    [INotifyPropertyChanged]
    public sealed partial class UnsupportedFilePreview : UserControl
    {
        [ObservableProperty]
        private ImageSource? iconPreview;

        [ObservableProperty]
        private string? fileName;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(FormattedFileType))]
        private string? fileType;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(FormattedFileSize))]
        private string? fileSize;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(FormattedDateModified))]
        private string? dateModified;

        public string FormattedFileType => ReadableStringHelper.FormatResourceString("UnsupportedFile_FileType", FileType);

        public string FormattedFileSize => ReadableStringHelper.FormatResourceString("UnsupportedFile_FileSize", FileSize);

        public string FormattedDateModified => ReadableStringHelper.FormatResourceString("UnsupportedFile_DateModified", DateModified);

        public UnsupportedFilePreview()
        {
            this.InitializeComponent();
        }
    }
}
