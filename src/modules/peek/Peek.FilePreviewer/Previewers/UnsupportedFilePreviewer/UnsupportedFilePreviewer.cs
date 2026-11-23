// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.IO;
using System.IO.Enumeration;
using System.Threading;
using System.Threading.Tasks;

using CommunityToolkit.Mvvm.ComponentModel;
using ManagedCommon;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Media.Imaging;
using Peek.Common.Extensions;
using Peek.Common.Helpers;
using Peek.Common.Models;
using Peek.FilePreviewer.Models;
using Peek.FilePreviewer.Previewers.Helpers;
using Windows.Foundation;

namespace Peek.FilePreviewer.Previewers
{
    public partial class UnsupportedFilePreviewer : ObservableObject, IUnsupportedFilePreviewer
    {
        /// <summary>
        /// The maximum view updates per second when enumerating a folder's contents.
        /// </summary>
        private const int MaxUpdateFps = 15;

        /// <summary>
        /// The icon to display when a file or folder's thumbnail or icon could not be retrieved.
        /// </summary>
        private static readonly SvgImageSource DefaultIcon = new(new Uri("ms-appx:///Assets/Peek/DefaultFileIcon.svg"));

        /// <summary>
        /// The options to use for the folder size enumeration. We recurse through all files and all subfolders.
        /// </summary>
        private static readonly EnumerationOptions FolderEnumerationOptions;

        [ObservableProperty]
        private UnsupportedFilePreviewData preview = new();

        [ObservableProperty]
        private PreviewState state;

        static UnsupportedFilePreviewer()
        {
            FolderEnumerationOptions = new()
            {
                RecurseSubdirectories = true,
                AttributesToSkip = FileAttributes.ReparsePoint,
                IgnoreInaccessible = true,
            };
        }

        public UnsupportedFilePreviewer(IFileSystemItem file)
        {
            Item = file;
            Dispatcher = DispatcherQueue.GetForCurrentThread();
        }

        private IFileSystemItem Item { get; }

        private DispatcherQueue Dispatcher { get; }

        public Task<PreviewSize> GetPreviewSizeAsync(CancellationToken cancellationToken) =>
            Task.FromResult(new PreviewSize { MonitorSize = new Size(680, 500), UseEffectivePixels = true });

        public async Task LoadPreviewAsync(CancellationToken cancellationToken)
        {
            try
            {
                await Dispatcher.RunOnUiThread(async () =>
                {
                    Preview.FileName = Item.Name;
                    Preview.DateModified = Item.DateModified?.ToString(CultureInfo.CurrentCulture);
                    Preview.IsFolder = Item is FolderItem;

                    State = PreviewState.Loaded;

                    await LoadIconPreviewAsync(cancellationToken);
                });

                var progress = new Progress<FolderScanProgress>(update =>
                {
                    Dispatcher.TryEnqueue(() =>
                    {
                        Preview.FileSize = ReadableStringHelper.BytesToReadableString(update.TotalBytes);
                        Preview.FolderContents = ReadableStringHelper.FormatFolderContents(
                            update.FileCount,
                            update.DirectoryCount,
                            update.State == FolderScanState.Scanning,
                            update.State == FolderScanState.PartialError);
                        Preview.FolderScanState = update.State;
                    });
                });

                await LoadDisplayInfoAsync(progress, cancellationToken);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                Logger.LogError("UnsupportedFilePreviewer error.", ex);
                State = PreviewState.Error;
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

        private async Task LoadIconPreviewAsync(CancellationToken cancellationToken)
        {
            Preview.IconPreview = await ThumbnailHelper.GetThumbnailAsync(Item.Path, cancellationToken) ??
                await ThumbnailHelper.GetIconAsync(Item.Path, cancellationToken) ??
                DefaultIcon;
        }

        private async Task LoadDisplayInfoAsync(IProgress<FolderScanProgress> sizeProgress, CancellationToken cancellationToken)
        {
            string type = await Item.GetContentTypeAsync();

            Dispatcher.TryEnqueue(() => Preview.FileType = type);

            if (Item is FolderItem)
            {
                await Task.Run(() => CalculateFolderSizeWithProgress(Item.Path, sizeProgress, cancellationToken), cancellationToken);
            }
            else
            {
                sizeProgress.Report(new FolderScanProgress(Item.FileSizeBytes, 0, 0, FolderScanState.Completed));
            }
        }

        private void CalculateFolderSizeWithProgress(string path, IProgress<FolderScanProgress> progress, CancellationToken cancellationToken)
        {
            ulong folderSize = 0;
            ulong fileCount = 0;
            ulong directoryCount = 0;
            bool hadError = false;

            TimeSpan updateInterval = TimeSpan.FromMilliseconds(1000 / MaxUpdateFps);

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            TimeSpan nextUpdate = updateInterval;

            try
            {
                var enumerable = new FileSystemEnumerable<(long Length, bool IsDirectory)>(
                    path,
                    (ref FileSystemEntry entry) => (entry.Length, entry.IsDirectory),
                    FolderEnumerationOptions);

                foreach (var (length, isDirectory) in enumerable)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (isDirectory)
                    {
                        directoryCount++;
                    }
                    else
                    {
                        fileCount++;
                        if (length > 0)
                        {
                            folderSize += (ulong)length;
                        }
                    }

                    if (stopwatch.Elapsed >= nextUpdate)
                    {
                        progress.Report(new FolderScanProgress(folderSize, fileCount, directoryCount, FolderScanState.Scanning));
                        nextUpdate = stopwatch.Elapsed + updateInterval;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception)
            {
                hadError = true;
                Logger.LogDebug("Error calculating folder size for directory: " + path);
            }

            var finalState = hadError ? FolderScanState.PartialError : FolderScanState.Completed;
            progress.Report(new FolderScanProgress(folderSize, fileCount, directoryCount, finalState));
        }
    }
}
