// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Peek.FilePreviewer.Previewers
{
    using System;
    using System.Drawing.Imaging;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using CommunityToolkit.Mvvm.ComponentModel;
    using Microsoft.UI.Dispatching;
    using Microsoft.UI.Xaml.Controls;
    using Microsoft.UI.Xaml.Media.Imaging;
    using Peek.Common;
    using Peek.Common.Extensions;
    using Windows.Foundation;
    using File = Peek.Common.Models.File;

    public partial class ImagePreviewer : ObservableObject, IBitmapPreviewer, IDisposable
    {
        [ObservableProperty]
        private BitmapSource? preview;

        [ObservableProperty]
        private PreviewState? state;

        public ImagePreviewer(File file)
        {
            File = file;
            Dispatcher = DispatcherQueue.GetForCurrentThread();

            PropertyChanged += OnPropertyChanged;
        }

        private File File { get; }

        private DispatcherQueue Dispatcher { get; }

        private Task? LowQualityThumbnailTask { get; set; }

        private Task? HighQualityThumbnailTask { get; set; }

        private Task? FullQualityImageTask { get; set; }

        private bool IsHighQualityThumbnailLoaded => HighQualityThumbnailTask?.Status == TaskStatus.RanToCompletion;

        private bool IsFullImageLoaded => FullQualityImageTask?.Status == TaskStatus.RanToCompletion;

        private CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();

        private CancellationToken CancellationToken => _cancellationTokenSource.Token;

        public void Dispose()
        {
            _cancellationTokenSource.Dispose();
            GC.SuppressFinalize(this);
        }

        public Task<Size> GetPreviewSizeAsync()
        {
            return WICHelper.GetImageSize(File.Path);
        }

        public async Task LoadPreviewAsync()
        {
            State = PreviewState.Loading;
            LowQualityThumbnailTask = LoadLowQualityThumbnailAsync();
            HighQualityThumbnailTask = LoadHighQualityThumbnailAsync();
            FullQualityImageTask = LoadFullQualityImageAsync();

            await Task.WhenAll(LowQualityThumbnailTask, HighQualityThumbnailTask, FullQualityImageTask);

            if (Preview == null && HasFailedLoadingPreview())
            {
                State = PreviewState.Error;
            }
        }

        private void OnPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(Preview))
            {
                if (Preview != null)
                {
                    State = PreviewState.Loaded;
                }
            }
        }

        private Task LoadLowQualityThumbnailAsync()
        {
            return Dispatcher.TryEnqueueSafe(async (tcs) =>
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

                tcs.SetResult();
            });
        }

        private Task LoadHighQualityThumbnailAsync()
        {
            return Dispatcher.TryEnqueueSafe(async (tcs) =>
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
                    Preview = thumbnailBitmap;
                }

                tcs.SetResult();
            });
        }

        private Task LoadFullQualityImageAsync()
        {
            return Dispatcher.TryEnqueueSafe(async (tcs) =>
            {
                // TODO: Check if this is performant
                var bitmap = await GetFullBitmapFromPathAsync(File.Path);

                if (CancellationToken.IsCancellationRequested)
                {
                    _cancellationTokenSource = new CancellationTokenSource();
                    return;
                }

                Preview = bitmap;
                tcs.SetResult();
            });
        }

        private bool HasFailedLoadingPreview()
        {
            var hasFailedLoadingLowQualityThumbnail = LowQualityThumbnailTask?.IsFaulted ?? false;
            var hasFailedLoadingHighQualityThumbnail = HighQualityThumbnailTask?.IsFaulted ?? false;
            var hasFailedLoadingFullQualityImage = FullQualityImageTask?.IsFaulted ?? false;

            return hasFailedLoadingLowQualityThumbnail && hasFailedLoadingHighQualityThumbnail && hasFailedLoadingFullQualityImage;
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
    }
}
