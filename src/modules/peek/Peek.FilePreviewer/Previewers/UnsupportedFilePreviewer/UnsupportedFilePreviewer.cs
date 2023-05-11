// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Media;
using Peek.Common.Extensions;
using Peek.Common.Helpers;
using Peek.Common.Models;
using Peek.FilePreviewer.Previewers.Helpers;
using Windows.Foundation;

namespace Peek.FilePreviewer.Previewers
{
    public partial class UnsupportedFilePreviewer : ObservableObject, IUnsupportedFilePreviewer, IDisposable
    {
        [ObservableProperty]
        private ImageSource? iconPreview;

        [ObservableProperty]
        private string? fileName;

        [ObservableProperty]
        private string? fileType;

        [ObservableProperty]
        private string? fileSize;

        [ObservableProperty]
        private string? dateModified;

        [ObservableProperty]
        private PreviewState state;

        public UnsupportedFilePreviewer(IFileSystemItem file)
        {
            Item = file;
            FileName = file.Name;
            DateModified = file.DateModified.ToString(CultureInfo.CurrentCulture);
            Dispatcher = DispatcherQueue.GetForCurrentThread();
        }

        public bool IsPreviewLoaded => iconPreview != null;

        private IFileSystemItem Item { get; }

        private DispatcherQueue Dispatcher { get; }

        private Task<bool>? IconPreviewTask { get; set; }

        private Task<bool>? DisplayInfoTask { get; set; }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }

        public Task<Size?> GetPreviewSizeAsync(CancellationToken cancellationToken)
        {
            Size? size = new Size(680, 500);
            return Task.FromResult(size);
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
        }

        public async Task CopyAsync()
        {
            await Dispatcher.RunOnUiThread(async () =>
            {
                var storageItem = await Item.GetStorageItemAsync();
                ClipboardHelper.SaveToClipboard(storageItem);
            });
        }

        public Task<bool> LoadIconPreviewAsync(CancellationToken cancellationToken)
        {
            return TaskExtension.RunSafe(async () =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                await Dispatcher.RunOnUiThread(async () =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var iconBitmap = await IconHelper.GetIconAsync(Path.GetFullPath(Item.Path), cancellationToken);
                    IconPreview = iconBitmap;
                });
            });
        }

        public Task<bool> LoadDisplayInfoAsync(CancellationToken cancellationToken)
        {
            return TaskExtension.RunSafe(async () =>
            {
                // File Properties
                cancellationToken.ThrowIfCancellationRequested();

                var bytes = await Task.Run(Item.GetSizeInBytes);

                cancellationToken.ThrowIfCancellationRequested();
                var type = await Task.Run(Item.GetContentTypeAsync);

                await Dispatcher.RunOnUiThread(() =>
                {
                    FileSize = ReadableStringHelper.BytesToReadableString(bytes);
                    FileType = type;
                    return Task.CompletedTask;
                });
            });
        }

        partial void OnIconPreviewChanged(ImageSource? value)
        {
            if (IconPreview != null)
            {
                State = PreviewState.Loaded;
            }
        }

        private bool HasFailedLoadingPreview()
        {
            var hasFailedLoadingIconPreview = !(IconPreviewTask?.Result ?? true);
            var hasFailedLoadingDisplayInfo = !(DisplayInfoTask?.Result ?? true);

            return hasFailedLoadingIconPreview && hasFailedLoadingDisplayInfo;
        }
    }
}
