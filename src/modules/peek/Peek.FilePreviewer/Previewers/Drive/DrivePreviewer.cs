// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml.Media.Imaging;
using Peek.Common.Helpers;
using Peek.Common.Models;
using Peek.FilePreviewer.Models;
using Peek.FilePreviewer.Previewers.Drive.Models;
using Peek.FilePreviewer.Previewers.Helpers;
using Peek.FilePreviewer.Previewers.Interfaces;
using Windows.Foundation;

namespace Peek.FilePreviewer.Previewers.Drive
{
    public partial class DrivePreviewer : ObservableObject, IDrivePreviewer
    {
        [ObservableProperty]
        private PreviewState _state;

        [ObservableProperty]
        private DrivePreviewData? _preview;

        private IFileSystemItem Item { get; }

        public DrivePreviewer(IFileSystemItem file)
        {
            Item = file;
        }

        public async Task CopyAsync()
        {
            // Nothing to copy for a drive
            await Task.CompletedTask;
        }

        public Task<PreviewSize> GetPreviewSizeAsync(CancellationToken cancellationToken)
        {
            var size = new Size(680, 400);
            var previewSize = new PreviewSize { MonitorSize = size, UseEffectivePixels = true };
            return Task.FromResult(previewSize);
        }

        public async Task LoadPreviewAsync(CancellationToken cancellationToken)
        {
            State = PreviewState.Loading;

            var preview = new DrivePreviewData();
            preview.Name = Item.Name;

            var drive = new DriveInfo(Item.Path);
            preview.Type = GetDriveTypeDescription(drive.DriveType);

            if (drive.IsReady)
            {
                preview.FileSystem = drive.DriveFormat;

                preview.Capacity = drive.TotalSize > 0
                    ? (ulong)drive.TotalSize
                    : 0;

                preview.FreeSpace = drive.AvailableFreeSpace > 0
                    ? (ulong)drive.AvailableFreeSpace
                    : 0;

                preview.UsedSpace = preview.Capacity - preview.FreeSpace;

                if (preview.Capacity > 0)
                {
                    preview.PercentageUsage = (double)preview.UsedSpace / preview.Capacity;
                }
            }
            else
            {
                preview.FileSystem = ResourceLoaderInstance.ResourceLoader.GetString("Drive_Unknown");
            }

            cancellationToken.ThrowIfCancellationRequested();
            var iconBitmap = await IconHelper.GetIconAsync(Item.Path, cancellationToken);
            preview.IconPreview = iconBitmap ?? new SvgImageSource(new Uri("ms-appx:///Assets/Peek/DefaultFileIcon.svg"));

            Preview = preview;
            State = PreviewState.Loaded;
        }

        public static bool IsItemSupported(IFileSystemItem item)
        {
            return DriveInfo.GetDrives().Any(d => d.Name == item.Path);
        }

        private string GetDriveTypeDescription(DriveType driveType) => driveType switch
        {
            DriveType.Unknown => ResourceLoaderInstance.ResourceLoader.GetString("Drive_Unknown"),
            DriveType.NoRootDirectory => ResourceLoaderInstance.ResourceLoader.GetString("Drive_Unknown"), // You shouldn't be able to preview an unmounted drives
            DriveType.Removable => ResourceLoaderInstance.ResourceLoader.GetString("Drive_Type_Removable"),
            DriveType.Fixed => ResourceLoaderInstance.ResourceLoader.GetString("Drive_Type_Fixed"),
            DriveType.Network => ResourceLoaderInstance.ResourceLoader.GetString("Drive_Type_Network"),
            DriveType.CDRom => ResourceLoaderInstance.ResourceLoader.GetString("Drive_Type_Optical"),
            DriveType.Ram => ResourceLoaderInstance.ResourceLoader.GetString("Drive_Type_RAM_Disk"),
            _ => ResourceLoaderInstance.ResourceLoader.GetString("Drive_Unknown"),
        };
    }
}
