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
using Microsoft.PowerToys.Telemetry;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Media.Imaging;
using Peek.Common.Extensions;
using Peek.Common.Helpers;
using Peek.Common.Models;
using Peek.FilePreviewer.Models;
using Peek.FilePreviewer.Previewers.Helpers;
using Peek.FilePreviewer.Previewers.Interfaces;
using Peek.UI.Telemetry.Events;
using Windows.Foundation;

namespace Peek.FilePreviewer.Previewers
{
    public partial class UnsupportedFilePreviewer : ObservableObject, IUnsupportedFilePreviewer, IReusablePreviewer
    {
        /// <summary>
        /// The maximum view updates per second when enumerating a folder's contents.
        /// </summary>
        private const int MaxUpdateFps = 15;

        /// <summary>
        /// Gets the icon to display when a file or folder's thumbnail or icon could not be retrieved.
        /// </summary>
        private static SvgImageSource? DefaultIcon
        {
            get
            {
                if (field is null)
                {
                    try
                    {
                        field = new SvgImageSource(new Uri("ms-appx:///Assets/Peek/DefaultFileIcon.svg"));
                    }
                    catch
                    {
                        // Test runner fallback.
                    }
                }

                return field;
            }
        }

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

            try
            {
                Dispatcher = DispatcherQueue.GetForCurrentThread();
            }
            catch
            {
                // For unit tests.
                Dispatcher = null;
            }
        }

        public IFileSystemItem Item { get; private set; }

        public void Rebind(IFileSystemItem item, double scalingFactor)
        {
            Item = item;

            // Update immediately-available metadata and clear async properties so stale
            // values from the previous item are not displayed during navigation.
            Preview.FileName = item.Name;
            Preview.DateModified = item.DateModified?.ToString(CultureInfo.CurrentCulture);
            Preview.FileType = null;
            Preview.FileSize = null;
            Preview.IconPreview = DefaultIcon;
        }

        private DispatcherQueue? Dispatcher { get; }

        private void EnqueueOnUIThread(Action action)
        {
            if (Dispatcher is not null)
            {
                Dispatcher.TryEnqueue(() => action());
            }
            else
            {
                // Direct execution in test runner.
                action();
            }
        }

        private async Task RunOnUIThreadAsync(Func<Task> action)
        {
            if (Dispatcher is not null)
            {
                await Dispatcher.RunOnUiThread(action);
            }
            else
            {
                // Direct execution in test runner.
                await action();
            }
        }

        public Task<PreviewSize> GetPreviewSizeAsync(CancellationToken cancellationToken) =>
            Task.FromResult(new PreviewSize { MonitorSize = new Size(680, 500), UseEffectivePixels = true });

        public async Task LoadPreviewAsync(CancellationToken cancellationToken)
        {
            var currentItem = Item;

            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (currentItem is not FolderItem)
                {
                    PowerToysTelemetry.Log.WriteEvent(
                        new ErrorEvent() { Failure = ErrorEvent.FailureType.FileNotSupported });
                }

                await RunOnUIThreadAsync(async () =>
                {
                    Preview.FileName = currentItem.Name;
                    Preview.DateModified = currentItem.DateModified?.ToString(CultureInfo.CurrentCulture);
                    Preview.IsFolder = currentItem is FolderItem;

                    State = PreviewState.Loaded;

                    await LoadIconPreviewAsync(currentItem, cancellationToken);
                });

                var progress = new Progress<FolderScanProgress>(update =>
                {
                    EnqueueOnUIThread(() =>
                    {
                        if (!cancellationToken.IsCancellationRequested && Item == currentItem)
                        {
                            Preview.FileSize =
                                ReadableStringHelper.BytesToReadableString(update.TotalBytes);
                            Preview.FolderContents = ReadableStringHelper.FormatFolderContents(
                                update.FileCount,
                                update.DirectoryCount,
                                update.State == FolderScanState.Scanning,
                                update.State == FolderScanState.PartialError);
                            Preview.FolderScanState = update.State;
                        }
                    });
                });

                await LoadDisplayInfoAsync(currentItem, progress, cancellationToken);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                if (!cancellationToken.IsCancellationRequested && Item == currentItem)
                {
                    Logger.LogError("UnsupportedFilePreviewer error.", ex);
                    State = PreviewState.Error;
                }
            }
        }

        public async Task CopyAsync()
        {
            await RunOnUIThreadAsync(async () =>
            {
                var storageItem = await Item.GetStorageItemAsync();
                ClipboardHelper.SaveToClipboard(storageItem);
            });
        }

        internal virtual async Task LoadIconPreviewAsync(IFileSystemItem item, CancellationToken cancellationToken)
        {
            Preview.IconPreview = await ThumbnailHelper.GetThumbnailAsync(item.Path, cancellationToken) ??
                await ThumbnailHelper.GetIconAsync(item.Path, cancellationToken) ??
                DefaultIcon;
        }

        internal virtual async Task<string> GetContentTypeAsync(IFileSystemItem item, CancellationToken cancellationToken) =>
            await item.GetContentTypeAsync();

        private async Task LoadDisplayInfoAsync(
            IFileSystemItem item, IProgress<FolderScanProgress> sizeProgress, CancellationToken cancellationToken)
        {
            string type = await GetContentTypeAsync(item, cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();

            EnqueueOnUIThread(() =>
            {
                if (!cancellationToken.IsCancellationRequested && Item == item)
                {
                    Preview.FileType = type;
                }
            });

            if (item is FolderItem folderItem)
            {
                await Task.Run(
                    () => CalculateFolderSizeWithProgress(folderItem.Path, sizeProgress, cancellationToken), cancellationToken);
            }
            else
            {
                sizeProgress.Report(new FolderScanProgress(item.FileSizeBytes, 0, 0, FolderScanState.Completed));
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
