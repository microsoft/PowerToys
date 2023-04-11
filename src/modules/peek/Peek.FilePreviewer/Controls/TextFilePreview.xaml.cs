// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml.Controls;

namespace Peek.FilePreviewer.Controls
{
    [INotifyPropertyChanged]
    public sealed partial class TextFilePreview : UserControl
    {
        [ObservableProperty]
        private string? fileName;

        [ObservableProperty]
        private string? fileContent;

        public TextFilePreview()
        {
            this.InitializeComponent();
        }
    }
}
