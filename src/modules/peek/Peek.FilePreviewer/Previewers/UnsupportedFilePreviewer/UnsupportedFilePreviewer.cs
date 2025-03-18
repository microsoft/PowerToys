// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.IO;
using System.Linq;
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
        /// The number of files to scan between updates when calculating folder size.
        /// </summary>
        private const int FolderEnumerationChunkSize = 100;

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
            FolderEnumerationOptions = new() { RecurseSubdirectories = true, AttributesToSkip = FileAttributes.ReparsePoint };
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

                    State = PreviewState.Loaded;

                    await LoadIconPreviewAsync(cancellationToken);
                });

                var progress = new Progress<string>(update =>
                {
                    Dispatcher.TryEnqueue(() =>
                    {
                        Preview.FileSize = update;
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

        private async Task LoadDisplayInfoAsync(IProgress<string> sizeProgress, CancellationToken cancellationToken)
        {
            string type = await Item.GetContentTypeAsync();

            Dispatcher.TryEnqueue(() => Preview.FileType = type);

            if (Item is FolderItem folderItem)
            {
                await Task.Run(() => CalculateFolderSizeWithProgress(Item.Path, sizeProgress, cancellationToken), cancellationToken);
            }
            else
            {
                ReportProgress(sizeProgress, Item.FileSizeBytes);
            }
        }

        private void CalculateFolderSizeWithProgress(string path, IProgress<string> progress, CancellationToken cancellationToken)
        {
            ulong folderSize = 0;
            TimeSpan updateInterval = TimeSpan.FromMilliseconds(1000 / MaxUpdateFps);
            DateTime nextUpdate = DateTime.UtcNow + updateInterval;

            var files = new DirectoryInfo(path).EnumerateFiles("*", FolderEnumerationOptions);

            foreach (var chunk in files.Chunk(FolderEnumerationChunkSize))
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (DateTime.Now >= nextUpdate)
                {
                    ReportProgress(progress, folderSize);
                    nextUpdate = DateTime.UtcNow + updateInterval;
                }

                foreach (var file in chunk)
                {
                    folderSize += (ulong)file.Length;
                }
            }

            ReportProgress(progress, folderSize);
        }

        private void ReportProgress(IProgress<string> progress, ulong size)
        {
            progress.Report(ReadableStringHelper.BytesToReadableString(size));
        }
    }
}
