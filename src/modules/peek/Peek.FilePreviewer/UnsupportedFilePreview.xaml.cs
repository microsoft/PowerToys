// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Peek.FilePreviewer
{
    using CommunityToolkit.Mvvm.ComponentModel;
    using Microsoft.UI.Xaml.Controls;
    using Microsoft.UI.Xaml.Media.Imaging;

    [INotifyPropertyChanged]
    public sealed partial class UnsupportedFilePreview : UserControl
    {
        [ObservableProperty]
        private BitmapSource? iconPreview;

        [ObservableProperty]
        private string? fileName;

        [ObservableProperty]
        private string? fileType;

        [ObservableProperty]
        private string? fileSize;

        [ObservableProperty]
        private string? dateModified;

        public UnsupportedFilePreview()
        {
            this.InitializeComponent();
        }
    }
}
