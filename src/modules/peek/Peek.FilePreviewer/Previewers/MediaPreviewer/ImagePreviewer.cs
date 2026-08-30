// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.PowerToys.FilePreviewCommon;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Peek.Common.Extensions;
using Peek.Common.Helpers;
using Peek.Common.Models;
using Peek.FilePreviewer.Models;
using Peek.FilePreviewer.Previewers.Helpers;
using Peek.FilePreviewer.Previewers.Interfaces;
using Windows.Foundation;
using Windows.Graphics.Imaging;

namespace Peek.FilePreviewer.Previewers
{
    public partial class ImagePreviewer : ObservableObject, IImagePreviewer, IReusablePreviewer
    {
        [ObservableProperty]
        private ImageSource? preview;

        [ObservableProperty]
        private PreviewState state;

        [ObservableProperty]
        private Size? imageSize;

        [ObservableProperty]
        private Size maxImageSize;

        [ObservableProperty]
        private double scalingFactor;

        private Size? _pendingImageSize;

        public ImagePreviewer(IFileSystemItem file)
        {
            Item = file;
            Dispatcher = DispatcherQueue.GetForCurrentThread();
        }

        public IFileSystemItem Item { get; private set; }

        public void Rebind(IFileSystemItem item, double scalingFactor)
        {
            Item = item;
            ScalingFactor = scalingFactor;
        }

        private bool IsPng() => Item.Extension == ".png";

        private bool IsQoi() => Item.Extension == ".qoi";

        private DispatcherQueue Dispatcher { get; }

        private static readonly HashSet<string> _supportedFileTypes =
            BitmapDecoder.GetDecoderInformationEnumerator()
                .SelectMany(di => di.FileExtensions)
                .Union([".qoi"])
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

        public static bool IsItemSupported(IFileSystemItem item)
        {
            return _supportedFileTypes.Contains(item.Extension);
        }

        public async Task<PreviewSize> GetPreviewSizeAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            Size? size;
            if (IsQoi())
            {
                size = await Task.Run(Item.GetQoiSize);
            }
            else
            {
                size = await Task.Run(Item.GetImageSize)
                    ?? await WICHelper.GetImageSize(Item.Path);
            }

            // If an image is already loaded (e.g. scaling factor changed on the current item),
            // update ImageSize immediately so MaxImageSize matches the new DPI scale.
            if (State == PreviewState.Loaded)
            {
                ImageSize = size;
            }

            _pendingImageSize = size;
            return new PreviewSize { MonitorSize = size };
        }

        public async Task LoadPreviewAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            State = PreviewState.Loading;

            bool loaded = await LoadFullQualityImageAsync(cancellationToken);

            if (!loaded && Preview is null)
            {
                loaded = await LoadThumbnailAsync(cancellationToken);
            }

            cancellationToken.ThrowIfCancellationRequested();
            if (loaded)
            {
                // Only commit ImageSize once loaded so MaxImageSize does not resize the
                // visible front-buffer image prematurely.
                ImageSize = _pendingImageSize;
                State = PreviewState.Loaded;
            }
            else
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

        partial void OnScalingFactorChanged(double value)
        {
            UpdateMaxImageSize();
        }

        partial void OnImageSizeChanged(Size? value)
        {
            UpdateMaxImageSize();
        }

        private void UpdateMaxImageSize()
        {
            double imageWidth = ImageSize?.Width ?? 0;
            double imageHeight = ImageSize?.Height ?? 0;

            MaxImageSize = ScalingFactor != 0 ?
                new Size(imageWidth / ScalingFactor, imageHeight / ScalingFactor) :
                new Size(imageWidth, imageHeight);
        }

        private Task<bool> LoadThumbnailAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return TaskExtension.RunSafe(async () =>
            {
                await Dispatcher.RunOnUiThread(async () =>
                {
                    Preview = await ThumbnailHelper.GetCachedThumbnailAsync(Item.Path, IsPng(), cancellationToken);
                });
            });
        }

        private Task<bool> LoadFullQualityImageAsync(CancellationToken cancellationToken)
        {
            return TaskExtension.RunSafe(async () =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                await Dispatcher.RunOnUiThread(async () =>
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (IsQoi())
                    {
                        using FileStream stream = ReadHelper.OpenReadOnly(Item.Path);
                        using var bitmap = QoiImage.FromStream(stream);

                        var source = await BitmapHelper.BitmapToImageSource(bitmap, true, cancellationToken);
                        cancellationToken.ThrowIfCancellationRequested();
                        Preview = source;
                    }
                    else
                    {
                        using FileStream stream = ReadHelper.OpenReadOnly(Item.Path);
                        var bmp = new BitmapImage();

                        await bmp.SetSourceAsync(stream.AsRandomAccessStream());
                        cancellationToken.ThrowIfCancellationRequested();
                        Preview = bmp;
                    }
                });
            });
        }
    }
}
