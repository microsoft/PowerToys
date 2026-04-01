// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml.Media;

namespace Peek.FilePreviewer.Previewers.Drive.Models
{
    public partial class DrivePreviewData : ObservableObject
    {
        [ObservableProperty]
        public partial ImageSource? IconPreview { get; set; }

        [ObservableProperty]
        public partial string Name { get; set; }

        [ObservableProperty]
        public partial string Type { get; set; }

        [ObservableProperty]
        public partial string FileSystem { get; set; }

        [ObservableProperty]
        public partial ulong Capacity { get; set; }

        [ObservableProperty]
        public partial ulong UsedSpace { get; set; }

        [ObservableProperty]
        public partial ulong FreeSpace { get; set; }

        /// <summary>
        /// Represents the usage percentage of the drive, ranging from 0 to 1
        /// </summary>
        [ObservableProperty]
        public partial double PercentageUsage { get; set; }

        public DrivePreviewData()
        {
            Name = string.Empty;
            Type = string.Empty;
            FileSystem = string.Empty;
        }
    }
}
