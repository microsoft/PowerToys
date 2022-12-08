// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Peek.FilePreviewer.Previewers
{
    using System;
    using System.Collections.Generic;
    using System.Drawing.Imaging;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using CommunityToolkit.Mvvm.ComponentModel;
    using Microsoft.UI.Dispatching;
    using Microsoft.UI.Xaml.Media.Imaging;
    using Peek.Common;
    using Windows.Foundation;
    using Windows.Graphics.Imaging;
    using Windows.Storage.Streams;
    using File = Peek.Common.Models.File;

    public partial class ImagePreviewer : ObservableObject, IBitmapPreviewer, IDisposable
    {
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsPreviewLoaded))]
        private BitmapSource? preview;

        public ImagePreviewer(File file)
        {
            File = file;
            Dispatcher = DispatcherQueue.GetForCurrentThread();
        }

        public bool IsPreviewLoaded => preview != null;

        private File File { get; }

        private DispatcherQueue Dispatcher { get; }

        private bool IsHighQualityThumbnailLoaded { get; set; }

        private bool IsFullImageLoaded { get; set; }

        private CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();

        private CancellationToken CancellationToken => _cancellationTokenSource.Token;

        public void Dispose()
        {
            _cancellationTokenSource.Dispose();
            GC.SuppressFinalize(this);
        }

        public async Task<Size> GetPreviewSizeAsync()
        {
            var propertyImageSize = await PropertyHelper.GetImageSize(File.Path);
            if (propertyImageSize != Size.Empty)
            {
                return propertyImageSize;
            }

            return await WICHelper.GetImageSize(File.Path);
        }

        public Task LoadPreviewAsync()
        {
            if (this.File.Extension == ".png")
            {
                var pngLowTask = LoadLowQualityPngAsync();
                var pngFullTask = LoadFullQualityPngAsync();

                return Task.WhenAll(pngLowTask, pngFullTask);
            }

            var lowQualityThumbnailTask = LoadLowQualityThumbnailAsync();
            var highQualityThumbnailTask = LoadHighQualityThumbnailAsync();
            var fullImageTask = LoadFullQualityImageAsync();

            return Task.WhenAll(lowQualityThumbnailTask, highQualityThumbnailTask, fullImageTask);
        }

        private Task LoadLowQualityPngAsync()
        {
            var thumbnailTCS = new TaskCompletionSource();

            Dispatcher.TryEnqueue(async () =>
            {
                if (CancellationToken.IsCancellationRequested)
                {
                    _cancellationTokenSource = new CancellationTokenSource();
                    return;
                }

                if (!IsFullImageLoaded && !IsHighQualityThumbnailLoaded)
                {
                    // TODO: Handle thumbnail errors
                    // Open a Stream and decode a PNG image
                    Stream stream = new FileStream(File.Path, FileMode.Open, FileAccess.Read, FileShare.Read);
                    IRandomAccessStream randomAccessStream = stream.AsRandomAccessStream();

                    // Create an encoder with the desired format
                    var encoder = await BitmapDecoder.CreateAsync(BitmapDecoder.PngDecoderId, randomAccessStream);

                    // preview image
                    var file = await Windows.Storage.StorageFile.GetFileFromPathAsync(File.Path);
                    var imageStream = await file.GetThumbnailAsync(
                        Windows.Storage.FileProperties.ThumbnailMode.SingleItem,
                        1280,
                        Windows.Storage.FileProperties.ThumbnailOptions.None);

                    BitmapImage bitmapImage = new BitmapImage();
                    bitmapImage.SetSource(imageStream);

                    Preview = bitmapImage;
                }

                thumbnailTCS.SetResult();
            });

            return thumbnailTCS.Task;
        }

        private Task LoadFullQualityPngAsync()
        {
            var thumbnailTCS = new TaskCompletionSource();
            Dispatcher.TryEnqueue(async () =>
            {
                if (CancellationToken.IsCancellationRequested)
                {
                    _cancellationTokenSource = new CancellationTokenSource();
                    return;
                }

                // TODO: Handle thumbnail errors
                Stream stream = new FileStream(File.Path, FileMode.Open, FileAccess.Read, FileShare.Read);
                IRandomAccessStream randomAccessStream = stream.AsRandomAccessStream();

                // Create an encoder with the desired format
                var encoder = await BitmapDecoder.CreateAsync(BitmapDecoder.PngDecoderId, randomAccessStream);

                // full quality image
                var softwareBitmap = await encoder.GetSoftwareBitmapAsync();
                WriteableBitmap bitmap = new WriteableBitmap(softwareBitmap.PixelWidth, softwareBitmap.PixelHeight);
                softwareBitmap.CopyToBuffer(bitmap.PixelBuffer);

                IsFullImageLoaded = true;

                Preview = bitmap;

                thumbnailTCS.SetResult();
            });

            return thumbnailTCS.Task;
        }

        private Task LoadLowQualityThumbnailAsync()
        {
            var thumbnailTCS = new TaskCompletionSource();
            Dispatcher.TryEnqueue(async () =>
            {
                if (CancellationToken.IsCancellationRequested)
                {
                    _cancellationTokenSource = new CancellationTokenSource();
                    return;
                }

                if (!IsFullImageLoaded && !IsHighQualityThumbnailLoaded)
                {
                    // TODO: Handle thumbnail errors
                    ThumbnailHelper.GetThumbnail(Path.GetFullPath(File.Path), out IntPtr hbitmap, ThumbnailHelper.LowQualityThumbnailSize);
                    var thumbnailBitmap = await GetBitmapFromHBitmapAsync(hbitmap);
                    Preview = thumbnailBitmap;
                }

                thumbnailTCS.SetResult();
            });

            return thumbnailTCS.Task;
        }

        private Task LoadHighQualityThumbnailAsync()
        {
            var thumbnailTCS = new TaskCompletionSource();
            Dispatcher.TryEnqueue(async () =>
            {
                if (CancellationToken.IsCancellationRequested)
                {
                    _cancellationTokenSource = new CancellationTokenSource();
                    return;
                }

                if (!IsFullImageLoaded)
                {
                    // TODO: Handle thumbnail errors
                    ThumbnailHelper.GetThumbnail(Path.GetFullPath(File.Path), out IntPtr hbitmap, ThumbnailHelper.HighQualityThumbnailSize);
                    var thumbnailBitmap = await GetBitmapFromHBitmapAsync(hbitmap);
                    IsHighQualityThumbnailLoaded = true;
                    Preview = thumbnailBitmap;
                }

                thumbnailTCS.SetResult();
            });

            return thumbnailTCS.Task;
        }

        private Task LoadFullQualityImageAsync()
        {
            var fullImageTCS = new TaskCompletionSource();
            Dispatcher.TryEnqueue(async () =>
            {
                // TODO: Check if this is performant
                var bitmap = await GetFullBitmapFromPathAsync(File.Path);
                IsFullImageLoaded = true;

                if (CancellationToken.IsCancellationRequested)
                {
                    _cancellationTokenSource = new CancellationTokenSource();
                    return;
                }

                Preview = bitmap;
                fullImageTCS.SetResult();
            });

            return fullImageTCS.Task;
        }

        private static async Task<BitmapImage> GetFullBitmapFromPathAsync(string path)
        {
            var bitmap = new BitmapImage();
            using (FileStream stream = System.IO.File.OpenRead(path))
            {
                await bitmap.SetSourceAsync(stream.AsRandomAccessStream());
            }

            return bitmap;
        }

        private static async Task<BitmapSource> GetBitmapFromHBitmapAsync(IntPtr hbitmap)
        {
            try
            {
                var bitmap = System.Drawing.Image.FromHbitmap(hbitmap);
                var bitmapImage = new BitmapImage();
                using (var stream = new MemoryStream())
                {
                    bitmap.Save(stream, ImageFormat.Bmp);
                    stream.Position = 0;
                    await bitmapImage.SetSourceAsync(stream.AsRandomAccessStream());
                }

                return bitmapImage;
            }
            finally
            {
                // delete HBitmap to avoid memory leaks
                NativeMethods.DeleteObject(hbitmap);
            }
        }

        public static bool IsFileTypeSupported(string fileExt)
        {
            return _supportedFileTypes.Contains(fileExt);
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
                ".tif",
                ".tiff",
                ".dib",

                // ".heic", // Error in System.Drawing.Image.FromHbitmap(hbitmap);
                ".heif",
                ".hif",
                ".avif",
                ".jxr",
                ".wdp",
                ".ico",
                ".thumb",

                // Raw types
                ".arw",
                ".cr2",

                // ".crw", // Error in WICImageFactory.CreateDecoderFromFilename
                // ".erf", // Error in WICImageFactory.CreateDecoderFromFilename
                ".kdc",
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
                ".dcs",
                ".dcr",
                ".drf",
                ".k25",
                ".3fr",
                ".ari",
                ".bay",
                ".cap",
                ".iiq",
                ".eip",
                ".fff",
                ".mef",
                ".mdc",
                ".mos",
                ".R3D",
                ".rwz",
                ".x3f",
                ".ori",
                ".cr3",
        };
    }
}
