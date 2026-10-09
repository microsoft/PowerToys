// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml.Media;
using Windows.Media.Core;

namespace Peek.FilePreviewer.Previewers.MediaPreviewer.Models
{
    public partial class AudioPreviewData : ObservableObject
    {
        [ObservableProperty]
        private MediaSource? _mediaSource;

        [ObservableProperty]
        private ImageSource? _thumbnail;

        [ObservableProperty]
        private string _title;

        [ObservableProperty]
        private string _artist;

        [ObservableProperty]
        private string _album;

        [ObservableProperty]
        private string _length;

        public AudioPreviewData()
        {
            Artist = string.Empty;
            Title = string.Empty;
            Album = string.Empty;
            Length = string.Empty;
        }
    }
}
