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
using ManagedCommon;
using Microsoft.PowerToys.FilePreviewCommon;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Peek.Common.Extensions;
using Peek.Common.Helpers;
using Peek.Common.Models;
using Peek.FilePreviewer.Exceptions;
using Peek.FilePreviewer.Models;
using Peek.FilePreviewer.Previewers.Helpers;
using Peek.FilePreviewer.Previewers.Interfaces;
using Windows.Foundation;
using Windows.Graphics.Imaging;

namespace Peek.FilePreviewer.Previewers
{
    public partial class ImagePreviewer : ObservableObject, IImagePreviewer
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

        public ImagePreviewer(IFileSystemItem file)
        {
            Item = file;
            Dispatcher = DispatcherQueue.GetForCurrentThread();
        }

        private IFileSystemItem Item { get; }

        private bool IsPng() => Item.Extension == ".png";

        private bool IsSvg() => Item.Extension == ".svg";

        private bool IsQoi() => Item.Extension == ".qoi";

        private DispatcherQueue Dispatcher { get; }

        private static readonly HashSet<string> _supportedFileTypes =
            BitmapDecoder.GetDecoderInformationEnumerator()
                .SelectMany(di => di.FileExtensions)
                .Union([".svg", ".qoi"])
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

        public static bool IsItemSupported(IFileSystemItem item)
        {
            return _supportedFileTypes.Contains(item.Extension);
        }

        public async Task<PreviewSize> GetPreviewSizeAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (IsSvg())
            {
                var size = await Task.Run(Item.GetSvgSize);
                if (size != null)
                {
                    ImageSize = size.Value;
                }
            }
            else if (IsQoi())
            {
                var size = await Task.Run(Item.GetQoiSize);
                if (size != null)
                {
                    ImageSize = size.Value;
                }
            }
            else
            {
                ImageSize = await Task.Run(Item.GetImageSize);
                if (ImageSize == null)
                {
                    ImageSize = await WICHelper.GetImageSize(Item.Path);
                }
            }

            return new PreviewSize { MonitorSize = ImageSize };
        }

        public async Task LoadPreviewAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            State = PreviewState.Loading;

            if (!await LoadFullQualityImageAsync(cancellationToken) &&
                !await LoadThumbnailAsync(cancellationToken))
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

        partial void OnPreviewChanged(ImageSource? value)
        {
            if (Preview != null)
            {
                State = PreviewState.Loaded;
            }
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

                    using FileStream stream = ReadHelper.OpenReadOnly(Item.Path);

                    if (IsSvg())
                    {
                        var source = new SvgImageSource();
                        source.RasterizePixelHeight = ImageSize?.Height ?? 0;
                        source.RasterizePixelWidth = ImageSize?.Width ?? 0;

                        var loadStatus = await source.SetSourceAsync(stream.AsRandomAccessStream());
                        if (loadStatus != SvgImageSourceLoadStatus.Success)
                        {
                            Logger.LogError("Error loading SVG: " + loadStatus.ToString());
                            throw new ImageLoadingException(nameof(source));
                        }

                        Preview = source;
                    }
                    else if (IsQoi())
                    {
                        using var bitmap = QoiImage.FromStream(stream);

                        Preview = await BitmapHelper.BitmapToImageSource(bitmap, true, cancellationToken);
                    }
                    else
                    {
                        Preview = new BitmapImage();
                        await ((BitmapImage)Preview).SetSourceAsync(stream.AsRandomAccessStream());
                    }
                });
            });
        }
    }
}
