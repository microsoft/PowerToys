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
using Peek.FilePreviewer.Models;
using Peek.FilePreviewer.Previewers.Helpers;
using Peek.FilePreviewer.Previewers.Interfaces;
using Windows.Foundation;
using Windows.Graphics.Imaging;

namespace Peek.FilePreviewer.Previewers
{
    /// <summary>
    /// Previews image files, resolving their intrinsic dimensions and committing the
    /// decoded source, size and state together.
    /// </summary>
    /// <remarks>
    /// All sizes exposed by this class (for example <see cref="ImageSize"/> and the
    /// results of <see cref="CalculateImageSizeAsync"/> / <see cref="TryGetSourcePixelSize"/>)
    /// are intrinsic source-image dimensions in whole pixels. They are represented as
    /// <see cref="Size"/> (double) only because that is the interop currency consumed by
    /// the sizing layer; no sub-pixel precision is implied. Fractional values first arise
    /// in <c>WindowConstants</c> when the display scale is applied, and are quantized
    /// back to device pixels by <c>WindowConstants.ToPhysicalPixels</c>.
    /// </remarks>
    public partial class ImagePreviewer : ObservableObject, IImagePreviewer, IReusablePreviewer
    {
        [ObservableProperty]
        private ImageSource? preview;

        [ObservableProperty]
        private PreviewState state = PreviewState.Uninitialized;

        [ObservableProperty]
        private Size? imageSize;

        [ObservableProperty]
        private double scalingFactor = 1.0;

        public ImagePreviewer(IFileSystemItem file)
        {
            Item = file;

            try
            {
                Dispatcher = DispatcherQueue.GetForCurrentThread();
            }
            catch
            {
                Dispatcher = null; // Unit test fallback
            }
        }

        public IFileSystemItem Item { get; private set; }

        private DispatcherQueue? Dispatcher { get; }

        public void Rebind(IFileSystemItem item, double scalingFactor)
        {
            Item = item;
            ScalingFactor = scalingFactor;

            // Transition to Loading so GetPreviewAsync for the new item does not
            // prematurely update ImageSize while the old item is visible.
            State = PreviewState.Loading;
        }

        private bool IsPng(IFileSystemItem item) => item.Extension == ".png";

        private bool IsQoi(IFileSystemItem item) => item.Extension == ".qoi";

        private static readonly HashSet<string> _supportedFileTypes =
            BitmapDecoder.GetDecoderInformationEnumerator()
                .SelectMany(di => di.FileExtensions)
                .Union([".qoi"])
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

        public static bool IsItemSupported(IFileSystemItem item)
        {
            return _supportedFileTypes.Contains(item.Extension);
        }

        internal virtual async Task<Size?> CalculateImageSizeAsync(
            IFileSystemItem item, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return IsQoi(item)
                ? await Task.Run(item.GetQoiSize, cancellationToken)
                : await Task.Run(item.GetImageSize, cancellationToken)
                    ?? await WICHelper.GetImageSize(item.Path);
        }

        // Reading PixelWidth/PixelHeight requires a fully activated WinUI BitmapImage,
        // so this is a seam that unit tests override to avoid COM activation on stubbed
        // image sources.
        internal virtual Size? TryGetSourcePixelSize(ImageSource source) =>
            source is BitmapImage bmp && bmp.PixelWidth > 0 && bmp.PixelHeight > 0
                ? new Size(bmp.PixelWidth, bmp.PixelHeight)
                : null;

        /// <summary>
        /// Calculates preview dimensions. If the item is already loaded and active on-
        /// screen (e.g. DPI/ScalingFactor changed), ImageSize is updated immediately.
        /// </summary>
        public async Task<PreviewSize> GetPreviewSizeAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var targetItem = Item;

            Size? size = await CalculateImageSizeAsync(targetItem, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            if (Item == targetItem)
            {
                // Update ImageSize immediately if the item is loaded.
                if (State == PreviewState.Loaded)
                {
                    ImageSize = size;
                }
            }

            return new PreviewSize { MonitorSize = size };
        }

        /// <summary>
        /// Decodes the target item and commits <see cref="Preview"/>,
        /// <see cref="ImageSize"/> and <see cref="State"/> together.
        /// </summary>
        public async Task LoadPreviewAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var targetItem = Item;
            State = PreviewState.Loading;

            var loadedSource = await LoadFullQualityImageAsync(targetItem, cancellationToken)
                ?? await LoadThumbnailImageAsync(targetItem, cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();

            // Resolve dimensions for the image. Prefer the decoded source's own pixel
            // dimensions and fall back to a separate size calculation when unavailable.
            Size? resolvedSize = loadedSource is not null
                ? TryGetSourcePixelSize(loadedSource) ?? await CalculateImageSizeAsync(targetItem, cancellationToken)
                : null;

            cancellationToken.ThrowIfCancellationRequested();

            // Commit on UI thread.
            if (Item == targetItem)
            {
                if (loadedSource is not null)
                {
                    ImageSize = resolvedSize;
                    Preview = loadedSource;
                    State = PreviewState.Loaded;
                }
                else
                {
                    Preview = null;
                    ImageSize = null;
                    State = PreviewState.Error;
                }
            }
        }

        public async Task CopyAsync()
        {
            if (Dispatcher is not null)
            {
                await Dispatcher.RunOnUiThread(async () =>
                {
                    var storageItem = await Item.GetStorageItemAsync();
                    ClipboardHelper.SaveToClipboard(storageItem);
                });
            }
            else
            {
                // For unit tests.
                var storageItem = await Item.GetStorageItemAsync();
                ClipboardHelper.SaveToClipboard(storageItem);
            }
        }

        internal virtual async Task<ImageSource?> LoadThumbnailImageAsync(
            IFileSystemItem item, CancellationToken cancellationToken)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                return await ThumbnailHelper.GetCachedThumbnailAsync(item.Path, IsPng(item), cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw; // Let cancellation propagate up to LoadPreviewAsync.
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to load thumbnail for {item.Path}", ex);
                return null;
            }
        }

        internal virtual async Task<ImageSource?> LoadFullQualityImageAsync(
            IFileSystemItem item, CancellationToken cancellationToken)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (IsQoi(item))
                {
                    using FileStream stream = ReadHelper.OpenReadOnly(item.Path);
                    using var bitmap = QoiImage.FromStream(stream);
                    return await BitmapHelper.BitmapToImageSource(bitmap, true, cancellationToken);
                }
                else
                {
                    using FileStream stream = ReadHelper.OpenReadOnly(item.Path);
                    var bmp = new BitmapImage();
                    await bmp.SetSourceAsync(stream.AsRandomAccessStream());
                    cancellationToken.ThrowIfCancellationRequested();
                    return (ImageSource)bmp;
                }
            }
            catch (OperationCanceledException)
            {
                throw; // Let cancellation propagate up to LoadPreviewAsync.
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to load full quality image for {item.Path}", ex);
                return null;
            }
        }
    }
}
