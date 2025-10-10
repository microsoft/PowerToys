// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using CommunityToolkit.Mvvm.ComponentModel;
using ManagedCommon;
using Microsoft.UI.Dispatching;
using Peek.Common.Extensions;
using Peek.Common.Helpers;
using Peek.Common.Models;
using Peek.FilePreviewer.Models;
using Peek.FilePreviewer.Previewers.Interfaces;
using Windows.Foundation;
using Windows.Media.Core;
using Windows.Media.MediaProperties;
using Windows.Media.Transcoding;
using Windows.Storage;

namespace Peek.FilePreviewer.Previewers
{
    public partial class VideoPreviewer : ObservableObject, IVideoPreviewer, IDisposable
    {
        private MediaSource? _mediaSource;

        [ObservableProperty]
        private MediaSource? preview;

        [ObservableProperty]
        private PreviewState state;

        [ObservableProperty]
        private Size videoSize;

        [ObservableProperty]
        private string? missingCodecName;

        public VideoPreviewer(IFileSystemItem file)
        {
            Item = file;
            Dispatcher = DispatcherQueue.GetForCurrentThread();
        }

        private IFileSystemItem Item { get; }

        private DispatcherQueue Dispatcher { get; }

        private Task<bool>? VideoTask { get; set; }

        public static bool IsItemSupported(IFileSystemItem item)
        {
            return _supportedFileTypes.Contains(item.Extension);
        }

        public void Dispose()
        {
            Unload();
            GC.SuppressFinalize(this);
        }

        public async Task LoadPreviewAsync(CancellationToken cancellationToken)
        {
            State = PreviewState.Loading;
            MissingCodecName = null;
            VideoTask = LoadVideoAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            await VideoTask;

            if (Preview == null && HasFailedLoadingPreview())
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

        public async Task<PreviewSize> GetPreviewSizeAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var videoSize = await Task.Run(Item.GetVideoSize);
            return new PreviewSize { MonitorSize = videoSize };
        }

        public async Task CopyAsync()
        {
            await Dispatcher.RunOnUiThread(async () =>
            {
                var storageItem = await Item.GetStorageItemAsync();
                ClipboardHelper.SaveToClipboard(storageItem);
            });
        }

        private async Task<string> GetMissingCodecAsync(StorageFile? file)
        {
            try
            {
                var profile = await MediaEncodingProfile.CreateFromFileAsync(file);

                if (profile.Video != null)
                {
                    var codecQuery = new CodecQuery();
                    var decoders = await codecQuery.FindAllAsync(
                        CodecKind.Video,
                        CodecCategory.Decoder,
                        profile.Video.Subtype);

                    return decoders.Count > 0 ? string.Empty : profile.Video.Subtype;
                }
                else
                {
                    Logger.LogWarning($"No video profile found for file {file?.Path}. Cannot determine codec support.");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error checking codec support for file {file?.Path}: {ex.Message}");
            }

            return string.Empty;
        }

        private Task<bool> LoadVideoAsync(CancellationToken cancellationToken)
        {
            return TaskExtension.RunSafe(async () =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                var storageFile = await Item.GetStorageItemAsync() as StorageFile;

                var missingCodecName = await GetMissingCodecAsync(storageFile);

                await Dispatcher.RunOnUiThread(() =>
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (!string.IsNullOrEmpty(missingCodecName))
                    {
                        MissingCodecName = missingCodecName;
                    }

                    _mediaSource = MediaSource.CreateFromStorageFile(storageFile);
                    Preview = _mediaSource;
                });
            });
        }

        private bool HasFailedLoadingPreview()
        {
            return !(VideoTask?.Result ?? true);
        }

        /// <summary>
        /// Explicitly unloads the preview and releases file resources.
        /// </summary>
        public void Unload()
        {
            _mediaSource?.Dispose();
            _mediaSource = null;
            Preview = null;
        }

        private static readonly HashSet<string> _supportedFileTypes = new()
        {
            ".mp4", ".3g2", ".3gp", ".3gp2", ".3gpp", ".asf", ".avi", ".m2t", ".m2ts",
            ".m4v", ".mkv", ".mov", ".mp4", ".mp4v", ".mts", ".wm", ".wmv", ".webm",
        };
    }
}
