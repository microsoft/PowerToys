// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
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
using Peek.FilePreviewer.Previewers.Interfaces;
using Peek.FilePreviewer.Previewers.MediaPreviewer.Models;
using Windows.Foundation;
using Windows.Media.Core;
using Windows.Storage;

namespace Peek.FilePreviewer.Previewers.MediaPreviewer
{
    public partial class AudioPreviewer : ObservableObject, IAudioPreviewer
    {
        [ObservableProperty]
        private PreviewState _state;

        [ObservableProperty]
        private AudioPreviewData _preview;

        private IFileSystemItem Item { get; }

        private DispatcherQueue Dispatcher { get; }

        public AudioPreviewer(IFileSystemItem file)
        {
            Item = file;
            Dispatcher = DispatcherQueue.GetForCurrentThread();
            Preview = new AudioPreviewData();
        }

        public async Task CopyAsync()
        {
            await Dispatcher.RunOnUiThread(async () =>
            {
                var storageItem = await Item.GetStorageItemAsync();
                ClipboardHelper.SaveToClipboard(storageItem);
            });
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

            var thumbnailTask = LoadThumbnailAsync(cancellationToken);
            var sourceTask = LoadSourceAsync(cancellationToken);
            var metadataTask = LoadMetadataAsync(cancellationToken);

            await Task.WhenAll(thumbnailTask, sourceTask, metadataTask);

            if (!thumbnailTask.Result || !sourceTask.Result || !metadataTask.Result)
            {
                State = PreviewState.Error;
            }
            else
            {
                State = PreviewState.Loaded;
            }
        }

        public Task<bool> LoadThumbnailAsync(CancellationToken cancellationToken)
        {
            return TaskExtension.RunSafe(async () =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                await Dispatcher.RunOnUiThread(async () =>
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var thumbnail = await IconHelper.GetThumbnailAsync(Item.Path, cancellationToken)
                        ?? await IconHelper.GetIconAsync(Item.Path, cancellationToken);

                    cancellationToken.ThrowIfCancellationRequested();

                    Preview.Thumbnail = thumbnail ?? new SvgImageSource(new Uri("ms-appx:///Assets/Peek/DefaultFileIcon.svg"));
                });
            });
        }

        private Task<bool> LoadSourceAsync(CancellationToken cancellationToken)
        {
            return TaskExtension.RunSafe(async () =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                var storageFile = await Item.GetStorageItemAsync() as StorageFile;

                await Dispatcher.RunOnUiThread(() =>
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    Preview.MediaSource = MediaSource.CreateFromStorageFile(storageFile);
                });
            });
        }

        private Task<bool> LoadMetadataAsync(CancellationToken cancellationToken)
        {
            return TaskExtension.RunSafe(async () =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                await Dispatcher.RunOnUiThread(() =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    Preview.Title = PropertyStoreHelper.TryGetStringProperty(Item.Path, PropertyKey.MusicTitle)
                        ?? Item.Name[..^Item.Extension.Length];

                    cancellationToken.ThrowIfCancellationRequested();
                    var artist = PropertyStoreHelper.TryGetStringProperty(Item.Path, PropertyKey.MusicDisplayArtist);
                    Preview.Artist = artist != null
                        ? string.Format(CultureInfo.CurrentCulture, ResourceLoaderInstance.ResourceLoader.GetString("Audio_Artist"), artist)
                        : string.Empty;

                    cancellationToken.ThrowIfCancellationRequested();
                    var album = PropertyStoreHelper.TryGetStringProperty(Item.Path, PropertyKey.MusicAlbum);
                    Preview.Album = album != null
                        ? string.Format(CultureInfo.CurrentCulture, ResourceLoaderInstance.ResourceLoader.GetString("Audio_Album"), album)
                        : string.Empty;

                    cancellationToken.ThrowIfCancellationRequested();
                    var ticksLength = PropertyStoreHelper.TryGetUlongProperty(Item.Path, PropertyKey.MusicDuration);
                    if (ticksLength.HasValue)
                    {
                        var length = TimeSpan.FromTicks((long)ticksLength);
                        var truncatedLength = new TimeSpan(length.Hours, length.Minutes, length.Seconds).ToString("g", CultureInfo.CurrentCulture);
                        Preview.Length = string.Format(CultureInfo.CurrentCulture, ResourceLoaderInstance.ResourceLoader.GetString("Audio_Length"), truncatedLength);
                    }
                    else
                    {
                        Preview.Length = string.Empty;
                    }
                });
            });
        }

        public static bool IsItemSupported(IFileSystemItem item)
        {
            return _supportedFileTypes.Contains(item.Extension);
        }

        private static readonly HashSet<string> _supportedFileTypes = new()
        {
            ".aac",
            ".ac3",
            ".amr",
            ".flac",
            ".m4a",
            ".mp3",
            ".ogg",
            ".wav",
            ".wma",
        };
    }
}
