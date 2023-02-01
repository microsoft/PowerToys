// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing.Imaging;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Media.Imaging;
using Peek.Common;
using Peek.Common.Extensions;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.Storage;
using Windows.Storage.Streams;
using File = Peek.Common.Models.File;

namespace Peek.FilePreviewer.Previewers
{
    public partial class ImagePreviewer : ObservableObject, IBitmapPreviewer, IDisposable
    {
        [ObservableProperty]
        private BitmapSource? preview;

        [ObservableProperty]
        private PreviewState state;

        [ObservableProperty]
        private Size imageSize;

        [ObservableProperty]
        private Size maxImageSize;

        [ObservableProperty]
        private double scalingFactor;

        public ImagePreviewer(File file)
        {
            File = file;
            Dispatcher = DispatcherQueue.GetForCurrentThread();

            PropertyChanged += OnPropertyChanged;
        }

        private File File { get; }

        private DispatcherQueue Dispatcher { get; }

        private Task<bool>? LowQualityThumbnailTask { get; set; }

        private Task<bool>? HighQualityThumbnailTask { get; set; }

        private Task<bool>? FullQualityImageTask { get; set; }

        private bool IsHighQualityThumbnailLoaded => HighQualityThumbnailTask?.Status == TaskStatus.RanToCompletion;

        private bool IsFullImageLoaded => FullQualityImageTask?.Status == TaskStatus.RanToCompletion;

        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }

        public async Task<Size?> GetPreviewSizeAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ImageSize = await PropertyHelper.GetImageSize(File.Path);
            if (ImageSize == Size.Empty)
            {
                ImageSize = await WICHelper.GetImageSize(File.Path);
            }

            return ImageSize;
        }

        public async Task LoadPreviewAsync(CancellationToken cancellationToken)
        {
            State = PreviewState.Loading;

            LowQualityThumbnailTask = LoadLowQualityThumbnailAsync(cancellationToken);
            HighQualityThumbnailTask = LoadHighQualityThumbnailAsync(cancellationToken);
            FullQualityImageTask = LoadFullQualityImageAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            await Task.WhenAll(LowQualityThumbnailTask, HighQualityThumbnailTask, FullQualityImageTask);

            if (Preview == null && HasFailedLoadingPreview())
            {
                State = PreviewState.Error;
            }
        }

        public async Task CopyAsync()
        {
            await Dispatcher.RunOnUiThread(async () =>
            {
                var storageFile = await File.GetStorageFileAsync();

                var dataPackage = new DataPackage();
                dataPackage.SetStorageItems(new StorageFile[1] { storageFile }, false);

                RandomAccessStreamReference imageStreamRef = RandomAccessStreamReference.CreateFromFile(storageFile);
                dataPackage.Properties.Thumbnail = imageStreamRef;
                dataPackage.SetBitmap(imageStreamRef);

                Clipboard.SetContent(dataPackage);
            });
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
            else if (e.PropertyName == nameof(ScalingFactor) || e.PropertyName == nameof(ImageSize))
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
        }

        private Task<bool> LoadLowQualityThumbnailAsync(CancellationToken cancellationToken)
        {
            return TaskExtension.RunSafe(async () =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                var hr = ThumbnailHelper.GetThumbnail(Path.GetFullPath(File.Path), out IntPtr hbitmap, ThumbnailHelper.LowQualityThumbnailSize);
                if (hr != Common.Models.HResult.Ok)
                {
                    Debug.WriteLine("Error loading low quality thumbnail - hresult: " + hr);

                    throw new ArgumentNullException(nameof(hbitmap));
                }

                cancellationToken.ThrowIfCancellationRequested();

                await Dispatcher.RunOnUiThread(async () =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var thumbnailBitmap = await GetBitmapFromHBitmapAsync(hbitmap, cancellationToken);
                    if (!IsFullImageLoaded && !IsHighQualityThumbnailLoaded)
                    {
                        Preview = thumbnailBitmap;
                    }
                });
            });
        }

        private Task<bool> LoadHighQualityThumbnailAsync(CancellationToken cancellationToken)
        {
            return TaskExtension.RunSafe(async () =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                var hr = ThumbnailHelper.GetThumbnail(Path.GetFullPath(File.Path), out IntPtr hbitmap, ThumbnailHelper.HighQualityThumbnailSize);
                if (hr != Common.Models.HResult.Ok)
                {
                    Debug.WriteLine("Error loading high quality thumbnail - hresult: " + hr);

                    throw new ArgumentNullException(nameof(hbitmap));
                }

                cancellationToken.ThrowIfCancellationRequested();

                await Dispatcher.RunOnUiThread(async () =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var thumbnailBitmap = await GetBitmapFromHBitmapAsync(hbitmap, cancellationToken);
                    if (!IsFullImageLoaded)
                    {
                        Preview = thumbnailBitmap;
                    }
                });
            });
        }

        private Task<bool> LoadFullQualityImageAsync(CancellationToken cancellationToken)
        {
            return TaskExtension.RunSafe(async () =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                // TODO: Check if this is performant
                await Dispatcher.RunOnUiThread(async () =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var bitmap = await GetFullBitmapFromPathAsync(File.Path, cancellationToken);
                    Preview = bitmap;
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

        private static async Task<BitmapImage> GetFullBitmapFromPathAsync(string path, CancellationToken cancellationToken)
        {
            var bitmap = new BitmapImage();

            cancellationToken.ThrowIfCancellationRequested();
            using (FileStream stream = System.IO.File.OpenRead(path))
            {
                cancellationToken.ThrowIfCancellationRequested();
                await bitmap.SetSourceAsync(stream.AsRandomAccessStream());
            }

            return bitmap;
        }

        private static async Task<BitmapSource> GetBitmapFromHBitmapAsync(IntPtr hbitmap, CancellationToken cancellationToken)
        {
            try
            {
                var bitmap = System.Drawing.Image.FromHbitmap(hbitmap);
                var bitmapImage = new BitmapImage();

                cancellationToken.ThrowIfCancellationRequested();
                using (var stream = new MemoryStream())
                {
                    bitmap.Save(stream, ImageFormat.Bmp);
                    stream.Position = 0;

                    cancellationToken.ThrowIfCancellationRequested();
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
        };
    }
}
