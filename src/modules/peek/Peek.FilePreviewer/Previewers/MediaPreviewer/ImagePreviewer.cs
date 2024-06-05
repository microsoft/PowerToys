// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using ManagedCommon;
using Microsoft.PowerToys.FilePreviewCommon;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Peek.Common;
using Peek.Common.Extensions;
using Peek.Common.Helpers;
using Peek.Common.Models;
using Peek.FilePreviewer.Exceptions;
using Peek.FilePreviewer.Models;
using Peek.FilePreviewer.Previewers.Helpers;
using Peek.FilePreviewer.Previewers.Interfaces;
using Windows.Foundation;

namespace Peek.FilePreviewer.Previewers
{
    public partial class ImagePreviewer : ObservableObject, IImagePreviewer, IDisposable
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

        private DispatcherQueue Dispatcher { get; }

        private Task<bool>? LowQualityThumbnailTask { get; set; }

        private Task<bool>? HighQualityThumbnailTask { get; set; }

        private Task<bool>? FullQualityImageTask { get; set; }

        private bool IsHighQualityThumbnailLoaded => HighQualityThumbnailTask?.Status == TaskStatus.RanToCompletion;

        private bool IsFullImageLoaded => FullQualityImageTask?.Status == TaskStatus.RanToCompletion;

        private IntPtr lowQualityThumbnail;

        private ImageSource? lowQualityThumbnailPreview;

        private IntPtr highQualityThumbnail;

        private ImageSource? highQualityThumbnailPreview;

        public static bool IsItemSupported(IFileSystemItem item)
        {
            return _supportedFileTypes.Contains(item.Extension);
        }

        public void Dispose()
        {
            Clear();
            GC.SuppressFinalize(this);
        }

        public async Task<PreviewSize> GetPreviewSizeAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (IsSvg(Item))
            {
                var size = await Task.Run(Item.GetSvgSize);
                if (size != null)
                {
                    ImageSize = size.Value;
                }
            }
            else if (IsQoi(Item))
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
            Clear();
            State = PreviewState.Loading;

            LowQualityThumbnailTask = LoadLowQualityThumbnailAsync(cancellationToken);
            HighQualityThumbnailTask = LoadHighQualityThumbnailAsync(cancellationToken);
            FullQualityImageTask = LoadFullQualityImageAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            await Task.WhenAll(LowQualityThumbnailTask, HighQualityThumbnailTask, FullQualityImageTask);

            // If Preview is still null, FullQualityImage was not available. Preview the thumbnail instead.
            if (Preview == null)
            {
                if (highQualityThumbnailPreview != null)
                {
                    Preview = highQualityThumbnailPreview;
                }
                else
                {
                    Preview = lowQualityThumbnailPreview;
                }
            }

            if (Preview == null && HasFailedLoadingPreview())
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
            var imageWidth = ImageSize?.Width ?? 0;
            var imageHeight = ImageSize?.Height ?? 0;

            if (ScalingFactor != 0)
            {
                MaxImageSize = new Size(imageWidth / ScalingFactor, imageHeight / ScalingFactor);
            }
            else
            {
                MaxImageSize = new Size(imageWidth, imageHeight);
            }
        }

        private Task<bool> LoadLowQualityThumbnailAsync(CancellationToken cancellationToken)
        {
            return TaskExtension.RunSafe(async () =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                var hr = ThumbnailHelper.GetThumbnail(Item.Path, out lowQualityThumbnail, ThumbnailHelper.LowQualityThumbnailSize);
                if (hr != HResult.Ok)
                {
                    Logger.LogError("Error loading low quality thumbnail - hresult: " + hr);
                    throw new ImageLoadingException(nameof(lowQualityThumbnail));
                }

                cancellationToken.ThrowIfCancellationRequested();

                await Dispatcher.RunOnUiThread(async () =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (!IsFullImageLoaded && !IsHighQualityThumbnailLoaded)
                    {
                        var thumbnailBitmap = await BitmapHelper.GetBitmapFromHBitmapAsync(lowQualityThumbnail, IsPng(Item), cancellationToken);
                        lowQualityThumbnailPreview = thumbnailBitmap;
                    }
                });
            });
        }

        private Task<bool> LoadHighQualityThumbnailAsync(CancellationToken cancellationToken)
        {
            return TaskExtension.RunSafe(async () =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                var hr = ThumbnailHelper.GetThumbnail(Item.Path, out highQualityThumbnail, ThumbnailHelper.HighQualityThumbnailSize);
                if (hr != HResult.Ok)
                {
                    Logger.LogError("Error loading high quality thumbnail - hresult: " + hr);
                    throw new ImageLoadingException(nameof(highQualityThumbnail));
                }

                cancellationToken.ThrowIfCancellationRequested();

                await Dispatcher.RunOnUiThread(async () =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (!IsFullImageLoaded)
                    {
                        var thumbnailBitmap = await BitmapHelper.GetBitmapFromHBitmapAsync(highQualityThumbnail, IsPng(Item), cancellationToken);
                        highQualityThumbnailPreview = thumbnailBitmap;
                    }
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

                    if (IsSvg(Item))
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
                    else if (IsQoi(Item))
                    {
                        using var bitmap = QoiImage.FromStream(stream);

                        Preview = await BitmapHelper.BitmapToImageSource(bitmap, true, cancellationToken);
                    }
                    else
                    {
                        var bitmap = new BitmapImage();
                        Preview = bitmap;
                        await bitmap.SetSourceAsync(stream.AsRandomAccessStream());
                    }
                });
            });
        }

        private bool HasFailedLoadingPreview()
        {
            var hasFailedLoadingLowQualityThumbnail = !(LowQualityThumbnailTask?.Result ?? true);
            var hasFailedLoadingHighQualityThumbnail = !(HighQualityThumbnailTask?.Result ?? true);
            var hasFailedLoadingFullQualityImage = !(FullQualityImageTask?.Result ?? true);

            return hasFailedLoadingLowQualityThumbnail && hasFailedLoadingHighQualityThumbnail && hasFailedLoadingFullQualityImage;
        }

        private bool IsPng(IFileSystemItem item)
        {
            return item.Extension == ".png";
        }

        private bool IsSvg(IFileSystemItem item)
        {
            return item.Extension == ".svg";
        }

        private bool IsQoi(IFileSystemItem item)
        {
            return item.Extension == ".qoi";
        }

        private void Clear()
        {
            lowQualityThumbnailPreview = null;
            highQualityThumbnailPreview = null;
            Preview = null;

            if (lowQualityThumbnail != IntPtr.Zero)
            {
                NativeMethods.DeleteObject(lowQualityThumbnail);
            }

            if (highQualityThumbnail != IntPtr.Zero)
            {
                NativeMethods.DeleteObject(highQualityThumbnail);
            }
        }

        private static readonly HashSet<string> _supportedFileTypes = new HashSet<string>
        {
                // Image types
                ".bmp",
                ".gif",
                ".jpg",
                ".jfif",
                ".jfi",
                ".jif",
                ".jpeg",
                ".jpe",
                ".png",
                ".tif",  // very slow for large files: no thumbnail?
                ".tiff", // NEED TO TEST
                ".dib",  // NEED TO TEST
                ".heic",
                ".heif",
                ".hif",  // NEED TO TEST
                ".avif", // NEED TO TEST
                ".jxr",
                ".wdp",
                ".ico",  // NEED TO TEST
                ".thumb", // NEED TO TEST
                ".webp",

                // Raw types
                ".arw",
                ".cr2",
                ".crw",
                ".erf",
                ".kdc", // NEED TO TEST
                ".mrw",
                ".nef",
                ".nrw",
                ".orf",
                ".pef",
                ".raf",
                ".raw",
                ".rw2",
                ".rwl",
                ".sr2",
                ".srw",
                ".srf",
                ".dcs", // NEED TO TEST
                ".dcr",
                ".drf", // NEED TO TEST
                ".k25",
                ".3fr",
                ".ari", // NEED TO TEST
                ".bay", // NEED TO TEST
                ".cap", // NEED TO TEST
                ".iiq",
                ".eip", // NEED TO TEST
                ".fff",
                ".mef",

                // ".mdc", // Crashes in GetFullBitmapFromPathAsync
                ".mos",
                ".R3D",
                ".rwz", // NEED TO TEST
                ".x3f",
                ".ori",
                ".cr3",

                ".svg",

                ".qoi",
        };
    }
}
