// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Peek.Common.Extensions;
using Peek.Common.Helpers;
using Peek.Common.Models;
using Peek.FilePreviewer.Exceptions;
using Peek.FilePreviewer.Previewers.Helpers;
using Peek.FilePreviewer.Previewers.Interfaces;
using Windows.Foundation;
using Windows.Media.Core;
using Windows.Storage;

namespace Peek.FilePreviewer.Previewers
{
    public partial class VideoPreviewer : ObservableObject, IVideoPreviewer, IDisposable
    {
        [ObservableProperty]
        private MediaSource? preview;

        [ObservableProperty]
        private PreviewState state;

        public VideoPreviewer(IFileSystemItem file)
        {
            Item = file;
            Dispatcher = DispatcherQueue.GetForCurrentThread();
        }

        private IFileSystemItem Item { get; }

        private DispatcherQueue Dispatcher { get; }

        private Task<bool>? VideoTask { get; set; }

        /*private bool IsVideoLoaded => VideoTask?.Status == TaskStatus.RanToCompletion;*/

        public static bool IsFileTypeSupported(string fileExt)
        {
            return _supportedFileTypes.Contains(fileExt);
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }

        public async Task LoadPreviewAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            State = PreviewState.Loading;
            await LoadVideoAsync(cancellationToken);

            if (HasFailedLoadingPreview())
            {
                State = PreviewState.Error;
            }
        }

        partial void OnPreviewChanged(MediaSource? value)
        {
            if (Preview != null)
            {
                State = PreviewState.Loaded;
            }
        }

        public async Task<Size?> GetPreviewSizeAsync(CancellationToken cancellationToken)
        {
            Size? size = await Task.Run(Item.GetItemDimensions);
            return size != null ? size.Value : null;
        }

        public async Task CopyAsync()
        {
            await Dispatcher.RunOnUiThread(async () =>
            {
                var storageItem = await Item.GetStorageItemAsync();
                ClipboardHelper.SaveToClipboard(storageItem);
            });
        }

        private Task<bool> LoadVideoAsync(CancellationToken cancellationToken)
        {
            return TaskExtension.RunSafe(async () =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                using FileStream stream = File.OpenRead(Item.Path);

                await Dispatcher.RunOnUiThread(async () =>
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var storageItem = await Item.GetStorageItemAsync();
                    Preview = MediaSource.CreateFromStorageFile(storageItem as StorageFile);
                });
            });
        }

        private bool HasFailedLoadingPreview()
        {
            return !(VideoTask?.Result ?? true);
        }

        private static readonly HashSet<string> _supportedFileTypes = new HashSet<string>
        {
            ".mp4", ".3g2", ".3gp", ".3gp2", ".3gpp", ".asf", ".avi", ".m2t", ".m2ts",
            ".m4v", ".mkv", ".mov", ".mp4", ".mp4v", ".mts", ".wm", ".wmv",
        };
    }
}
