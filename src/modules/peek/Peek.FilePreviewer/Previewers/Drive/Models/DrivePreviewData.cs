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
        private ImageSource? iconPreview;

        [ObservableProperty]
        private string _name;

        [ObservableProperty]
        private string _type;

        [ObservableProperty]
        private string _fileSystem;

        [ObservableProperty]
        private ulong _capacity;

        [ObservableProperty]
        private ulong _usedSpace;

        [ObservableProperty]
        private ulong _freeSpace;

        /// <summary>
        /// Represents the usage percentage of the drive, ranging from 0 to 1
        /// </summary>
        [ObservableProperty]
        private double _percentageUsage;

        public DrivePreviewData()
        {
            Name = string.Empty;
            Type = string.Empty;
            FileSystem = string.Empty;
        }
    }
}
