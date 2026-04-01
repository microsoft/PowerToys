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
        public partial MediaSource? MediaSource { get; set; }

        [ObservableProperty]
        public partial ImageSource? Thumbnail { get; set; }

        [ObservableProperty]
        public partial string Title { get; set; }

        [ObservableProperty]
        public partial string Artist { get; set; }

        [ObservableProperty]
        public partial string Album { get; set; }

        [ObservableProperty]
        public partial string Length { get; set; }

        public AudioPreviewData()
        {
            Artist = string.Empty;
            Title = string.Empty;
            Album = string.Empty;
            Length = string.Empty;
        }
    }
}
