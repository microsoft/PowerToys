// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using ManagedCommon;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media.Imaging;
using Peek.Common.Extensions;
using Peek.Common.Helpers;
using Peek.Common.Models;
using Peek.FilePreviewer.Models;
using Peek.FilePreviewer.Previewers.Helpers;
using Windows.Foundation;

namespace Peek.FilePreviewer.Previewers
{
    public partial class UnsupportedFilePreviewer : ObservableObject, IUnsupportedFilePreviewer, IDisposable
    {
        private static readonly EnumerationOptions _fileEnumOptions = new() { MatchType = MatchType.Win32, AttributesToSkip = 0, IgnoreInaccessible = true };
        private static readonly EnumerationOptions _directoryEnumOptions = new() { MatchType = MatchType.Win32, AttributesToSkip = FileAttributes.ReparsePoint, IgnoreInaccessible = true };
        private readonly DispatcherTimer _folderSizeDispatcherTimer = new();
        private ulong _folderSize;

        [ObservableProperty]
        private UnsupportedFilePreviewData preview = new UnsupportedFilePreviewData();

        [ObservableProperty]
        private PreviewState state;

        public UnsupportedFilePreviewer(IFileSystemItem file)
        {
            _folderSizeDispatcherTimer.Interval = TimeSpan.FromMilliseconds(500);
            _folderSizeDispatcherTimer.Tick += FolderSizeDispatcherTimer_Tick;

            Item = file;
            Preview.FileName = file.Name;
            Preview.DateModified = file.DateModified?.ToString(CultureInfo.CurrentCulture);
            Dispatcher = DispatcherQueue.GetForCurrentThread();
        }

        private IFileSystemItem Item { get; }

        private DispatcherQueue Dispatcher { get; }

        private Task<bool>? IconPreviewTask { get; set; }

        private Task<bool>? DisplayInfoTask { get; set; }

        public void Dispose()
        {
            _folderSizeDispatcherTimer.Tick -= FolderSizeDispatcherTimer_Tick;
            GC.SuppressFinalize(this);
        }

        public Task<PreviewSize> GetPreviewSizeAsync(CancellationToken cancellationToken)
        {
            Size? size = new Size(680, 500);
            var previewSize = new PreviewSize { MonitorSize = size, UseEffectivePixels = true };
            return Task.FromResult(previewSize);
        }

        public async Task LoadPreviewAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            State = PreviewState.Loading;

            IconPreviewTask = LoadIconPreviewAsync(cancellationToken);
            DisplayInfoTask = LoadDisplayInfoAsync(cancellationToken);

            await Task.WhenAll(IconPreviewTask, DisplayInfoTask);

            if (HasFailedLoadingPreview())
            {
                State = PreviewState.Error;
            }
            else
            {
                State = PreviewState.Loaded;
            }
        }

        public async Task CopyAsync()
        {
            await Dispatcher.RunOnUiThread(async () =>
            {
                var storageItem = await Item.GetStorageItemAsync();
                ClipboardHelper.SaveToClipboard(storageItem);
            });
        }

        public async Task<bool> LoadIconPreviewAsync(CancellationToken cancellationToken)
        {
            bool isIconValid = false;

            var isTaskSuccessful = await TaskExtension.RunSafe(async () =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                await Dispatcher.RunOnUiThread(async () =>
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var iconBitmap = await IconHelper.GetThumbnailAsync(Item.Path, cancellationToken)
                        ?? await IconHelper.GetIconAsync(Item.Path, cancellationToken);

                    cancellationToken.ThrowIfCancellationRequested();

                    isIconValid = iconBitmap != null;

                    Preview.IconPreview = iconBitmap ?? new SvgImageSource(new Uri("ms-appx:///Assets/Peek/DefaultFileIcon.svg"));
                });
            });

            return isIconValid && isTaskSuccessful;
        }

        public async Task<bool> LoadDisplayInfoAsync(CancellationToken cancellationToken)
        {
            bool isDisplayValid = false;

            var isTaskSuccessful = await TaskExtension.RunSafe(async () =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                var type = await Task.Run(Item.GetContentTypeAsync);

                cancellationToken.ThrowIfCancellationRequested();

                isDisplayValid = type != null;

                var readableFileSize = string.Empty;

                if (Item is FileItem)
                {
                    readableFileSize = ReadableStringHelper.BytesToReadableString(Item.FileSizeBytes);
                }
                else if (Item is FolderItem)
                {
                    ComputeFolderSize(cancellationToken);
                }

                await Dispatcher.RunOnUiThread(() =>
                {
                    Preview.FileSize = readableFileSize;
                    Preview.FileType = type;
                    return Task.CompletedTask;
                });
            });

            return isDisplayValid && isTaskSuccessful;
        }

        private bool HasFailedLoadingPreview()
        {
            var isLoadingIconPreviewSuccessful = IconPreviewTask?.Result ?? false;
            var isLoadingDisplayInfoSuccessful = DisplayInfoTask?.Result ?? false;

            return !isLoadingIconPreviewSuccessful || !isLoadingDisplayInfoSuccessful;
        }

        private void ComputeFolderSize(CancellationToken cancellationToken)
        {
            Task.Run(
            async () =>
            {
                try
                {
                    // Special folders like recycle bin don't have a path
                    if (string.IsNullOrWhiteSpace(Item.Path))
                    {
                        return;
                    }

                    await Dispatcher.RunOnUiThread(_folderSizeDispatcherTimer.Start);
                    GetDirectorySize(new DirectoryInfo(Item.Path), cancellationToken);
                }
                catch (OperationCanceledException)
                {
                }
                catch (Exception ex)
                {
                    Logger.LogError("Failed to calculate folder size", ex);
                }
                finally
                {
                    await Dispatcher.RunOnUiThread(_folderSizeDispatcherTimer.Stop);
                }

                // If everything went well, ensure the UI is updated
                await Dispatcher.RunOnUiThread(UpdateFolderSize);
            },
            cancellationToken);
        }

        private void GetDirectorySize(DirectoryInfo directory, CancellationToken cancellationToken)
        {
            var files = directory.GetFiles("*", _fileEnumOptions);
            for (var i = 0; i < files.Length; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var f = files[i];
                if (f.Length > 0)
                {
                    _folderSize += Convert.ToUInt64(f.Length);
                }
            }

            var directories = directory.GetDirectories("*", _directoryEnumOptions);
            for (var i = 0; i < directories.Length; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                GetDirectorySize(directories[i], cancellationToken);
            }
        }

        private void UpdateFolderSize()
        {
            Preview.FileSize = ReadableStringHelper.BytesToReadableString(_folderSize);
        }

        private void FolderSizeDispatcherTimer_Tick(object? sender, object e)
        {
            UpdateFolderSize();
        }
    }
}
