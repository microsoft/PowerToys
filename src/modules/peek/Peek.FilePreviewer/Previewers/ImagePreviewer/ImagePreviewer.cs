// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Peek.FilePreviewer.Previewers
{
    using System;
    using System.Diagnostics;
    using System.Drawing.Imaging;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using CommunityToolkit.Mvvm.ComponentModel;
    using Microsoft.UI.Dispatching;
    using Microsoft.UI.Xaml.Media.Imaging;
    using Peek.Common;
    using Windows.Foundation;
    using File = Peek.Common.Models.File;

    [INotifyPropertyChanged]
    public partial class ImagePreviewer : IDisposable
    {
        [ObservableProperty]
        private BitmapSource? preview;

        public ImagePreviewer(File file)
        {
            File = file;
            Dispatcher = DispatcherQueue.GetForCurrentThread();
        }

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

        public Task<Size> GetPreviewSizeAsync()
        {
            return WICHelper.GetImageSize(File.Path);
        }

        public Task LoadPreviewAsync()
        {
            var lowQualityThumbnailTask = LoadLowQualityThumbnailAsync();
            var highQualityThumbnailTask = LoadHighQualityThumbnailAsync();
            var fullImageTask = LoadFullQualityImageAsync();

            return Task.WhenAll(lowQualityThumbnailTask, highQualityThumbnailTask, fullImageTask);
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
                    var hr = ThumbnailHelper.GetThumbnail(Path.GetFullPath(File.Path), out IntPtr hbitmap, ThumbnailHelper.LowQualityThumbnailSize);
                    if (hr == Common.Models.HResult.Ok)
                    {
                        var thumbnailBitmap = await GetBitmapFromHBitmapAsync(hbitmap);
                        Preview = thumbnailBitmap;
                    }
                    else
                    {
                        // TODO: handle thumbnail errors
                        Debug.WriteLine("Error loading thumbnail - hresult: " + hr);
                    }
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
                    var hr = ThumbnailHelper.GetThumbnail(Path.GetFullPath(File.Path), out IntPtr hbitmap, ThumbnailHelper.HighQualityThumbnailSize);
                    if (hr == Common.Models.HResult.Ok)
                    {
                        var thumbnailBitmap = await GetBitmapFromHBitmapAsync(hbitmap);
                        IsHighQualityThumbnailLoaded = true;
                        Preview = thumbnailBitmap;
                    }
                    else
                    {
                        // TODO: handle thumbnail errors
                        Debug.WriteLine("Error loading thumbnail - hresult: " + hr);
                    }
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
                Debug.WriteLine("!~ fetching bitmap: " + hbitmap);
                var bitmap = System.Drawing.Image.FromHbitmap(hbitmap);
                var bitmapImage = new BitmapImage();
                using (var stream = new MemoryStream())
                {
                    bitmap.Save(stream, ImageFormat.Bmp);
                    stream.Position = 0;
                    Debug.WriteLine("!~ about to await setting bitmap: " + hbitmap);
                    await bitmapImage.SetSourceAsync(stream.AsRandomAccessStream());
                }

                return bitmapImage;
            }
            finally
            {
                Debug.WriteLine("!~ deleting " + hbitmap);

                // delete HBitmap to avoid memory leaks
                NativeMethods.DeleteObject(hbitmap);
            }
        }
    }
}
