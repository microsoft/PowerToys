// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Dispatching;
using Peek.Common.Constants;
using Peek.Common.Extensions;
using Peek.Common.Helpers;
using Peek.Common.Models;
using Peek.FilePreviewer.Previewers.Interfaces;
using Windows.Foundation;
using Windows.Media.Core;
using Windows.Storage;

namespace Peek.FilePreviewer.Previewers
{
    public partial class VideoPreviewer : ObservableObject, IVideoPreviewer, IDisposable
    {
        private static readonly HashSet<string> _supportedFileTypes = new HashSet<string>
        {
            ".mp4", ".3g2", ".3gp", ".3gp2", ".3gpp", ".asf", ".avi", ".m2t", ".m2ts",
            ".m4v", ".mkv", ".mov", ".mp4", ".mp4v", ".mts", ".wm", ".wmv",
        };

        [ObservableProperty]
        private MediaSource? preview;

        [ObservableProperty]
        private PreviewState state;

        public VideoPreviewer(IFileSystemItem file)
        {
            File = file;
            Dispatcher = DispatcherQueue.GetForCurrentThread();
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }

        private IFileSystemItem File { get; }

        public bool IsPreviewLoaded => preview != null;

        private DispatcherQueue Dispatcher { get; }

        private Task<bool>? DisplayInfoTask { get; set; }

        public Task<Size?> GetPreviewSizeAsync(CancellationToken cancellationToken)
        {
            Size? size = null;
            return Task.FromResult(size);
        }

        public async Task LoadPreviewAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            State = PreviewState.Loading;
            await LoadDisplayInfoAsync(cancellationToken);

            if (HasFailedLoadingPreview())
            {
                State = PreviewState.Error;
            }
        }

        public Task<bool> LoadDisplayInfoAsync(CancellationToken cancellationToken)
        {
            return TaskExtension.RunSafe(async () =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                await Dispatcher.RunOnUiThread(async () =>
                {
                    var storageItem = await File.GetStorageItemAsync();
                    Preview = MediaSource.CreateFromStorageFile(storageItem as StorageFile);
                });
            });
        }

        public async Task CopyAsync()
        {
            await Dispatcher.RunOnUiThread(async () =>
            {
                var storageItem = await File.GetStorageItemAsync();
                ClipboardHelper.SaveToClipboard(storageItem);
            });
        }

        public static bool IsFileTypeSupported(string fileExt)
        {
            return _supportedFileTypes.Contains(fileExt);
        }

        private bool HasFailedLoadingPreview()
        {
            return !(DisplayInfoTask?.Result ?? true);
        }
    }
}
