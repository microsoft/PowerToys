// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
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
    public partial class UnsupportedFilePreviewer : ObservableObject, IUnsupportedFilePreviewer, IDisposable
    {
        [ObservableProperty]
        private UnsupportedFilePreviewData preview = new UnsupportedFilePreviewData();

        [ObservableProperty]
        private PreviewState state;

        public UnsupportedFilePreviewer(IFileSystemItem file)
        {
            Item = file;
            Preview.FileName = file.Name;
            Preview.DateModified = file.DateModified?.ToString(CultureInfo.CurrentCulture);
            Dispatcher = DispatcherQueue.GetForCurrentThread();
        }

        public bool IsPreviewLoaded => Preview.IconPreview != null;

        private IFileSystemItem Item { get; }

        private DispatcherQueue Dispatcher { get; }

        private Task<bool>? IconPreviewTask { get; set; }

        private Task<bool>? DisplayInfoTask { get; set; }

        public void Dispose()
        {
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
                // File Properties
                cancellationToken.ThrowIfCancellationRequested();

                var bytes = await Task.Run(Item.GetSizeInBytes);

                cancellationToken.ThrowIfCancellationRequested();

                var type = await Task.Run(Item.GetContentTypeAsync);

                cancellationToken.ThrowIfCancellationRequested();

                var readableFileSize = ReadableStringHelper.BytesToReadableString(bytes);

                isDisplayValid = type != null;

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
    }
}
