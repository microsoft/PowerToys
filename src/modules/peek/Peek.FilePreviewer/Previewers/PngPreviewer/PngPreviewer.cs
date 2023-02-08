// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Media.Imaging;
using Peek.Common.Extensions;
using Peek.Common.Helpers;
using Peek.Common.Models;
using Windows.Foundation;
using Windows.Graphics.Imaging;
using Windows.Storage;

namespace Peek.FilePreviewer.Previewers
{
    public partial class PngPreviewer : ObservableObject, IBitmapPreviewer
    {
        private readonly uint _png_image_size = 1280;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsPreviewLoaded))]
        private BitmapSource? preview;

        [ObservableProperty]
        private PreviewState state;

        [ObservableProperty]
        private Size imageSize;

        [ObservableProperty]
        private Size maxImageSize;

        [ObservableProperty]
        private double scalingFactor;

        public PngPreviewer(IFileSystemItem file)
        {
            Item = file;
            Dispatcher = DispatcherQueue.GetForCurrentThread();
        }

        public bool IsPreviewLoaded => preview != null;

        private IFileSystemItem Item { get; }

        private DispatcherQueue Dispatcher { get; }

        private Task<bool>? PreviewQualityThumbnailTask { get; set; }

        private Task<bool>? FullQualityImageTask { get; set; }

        private bool IsFullImageLoaded => FullQualityImageTask?.Status == TaskStatus.RanToCompletion;

        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }

        public async Task<Size?> GetPreviewSizeAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ImageSize = await Task.Run(Item.GetImageSize);
            if (ImageSize == Size.Empty)
            {
                ImageSize = await WICHelper.GetImageSize(Item.Path);
            }

            return ImageSize;
        }

        public async Task LoadPreviewAsync(CancellationToken cancellationToken)
        {
            State = PreviewState.Loading;

            PreviewQualityThumbnailTask = LoadPreviewImageAsync(cancellationToken);
            FullQualityImageTask = LoadFullImageAsync(cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();
            await Task.WhenAll(PreviewQualityThumbnailTask, FullQualityImageTask);

            if (Preview == null)
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

        public static bool IsFileTypeSupported(string fileExt)
        {
            return fileExt == ".png" ? true : false;
        }

        partial void OnPreviewChanged(BitmapSource? value)
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

        partial void OnImageSizeChanged(Size value)
        {
            UpdateMaxImageSize();
        }

        private void UpdateMaxImageSize()
        {
            if (ScalingFactor != 0)
            {
                MaxImageSize = new Size(ImageSize.Width / ScalingFactor, ImageSize.Height / ScalingFactor);
            }
            else
            {
                MaxImageSize = new Size(ImageSize.Width, ImageSize.Height);
            }
        }

        private Task<bool> LoadPreviewImageAsync(CancellationToken cancellationToken)
        {
            var thumbnailTCS = new TaskCompletionSource();
            return TaskExtension.RunSafe(async () =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                await Dispatcher.RunOnUiThread(async () =>
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (Item is not FileItem fileItem)
                    {
                        return;
                    }

                    var storageFile = await fileItem.GetStorageFileAsync();

                    var thumbnail = await ThumbnailHelper.GetThumbnailAsync(storageFile, _png_image_size);
                    if (!IsFullImageLoaded)
                    {
                        Preview = thumbnail;
                    }
                });
            });
        }

        private Task<bool> LoadFullImageAsync(CancellationToken cancellationToken)
        {
            var thumbnailTCS = new TaskCompletionSource();
            return TaskExtension.RunSafe(async () =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                await Dispatcher.RunOnUiThread(async () =>
                {
                    WriteableBitmap? bitmap = null;

                    cancellationToken.ThrowIfCancellationRequested();
                    var storageItem = await Item.GetStorageItemAsync();

                    if (storageItem is not StorageFile storageFile)
                    {
                        Preview = null;
                        return;
                    }

                    cancellationToken.ThrowIfCancellationRequested();
                    using (var randomAccessStream = await storageFile.OpenStreamForReadAsync())
                    {
                        // Create an encoder with the desired format
                        cancellationToken.ThrowIfCancellationRequested();
                        var decoder = await BitmapDecoder.CreateAsync(
                            BitmapDecoder.PngDecoderId,
                            randomAccessStream.AsRandomAccessStream());

                        cancellationToken.ThrowIfCancellationRequested();
                        var softwareBitmap = await decoder.GetSoftwareBitmapAsync(
                            BitmapPixelFormat.Bgra8,
                            BitmapAlphaMode.Premultiplied);

                        // full quality image
                        bitmap = new WriteableBitmap((int)decoder.PixelWidth, (int)decoder.PixelHeight);
                        cancellationToken.ThrowIfCancellationRequested();
                        softwareBitmap?.CopyToBuffer(bitmap.PixelBuffer);
                    }

                    Preview = bitmap;
                });
            });
        }
    }
}
