// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
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
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;
using File = Peek.Common.Models.File;

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

        public PngPreviewer(File file)
        {
            File = file;
            Dispatcher = DispatcherQueue.GetForCurrentThread();

            PropertyChanged += OnPropertyChanged;
        }

        public bool IsPreviewLoaded => preview != null;

        private File File { get; }

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
            var propertyImageSize = await PropertyHelper.GetImageSize(File.Path);
            if (propertyImageSize != Size.Empty)
            {
                return propertyImageSize;
            }

            cancellationToken.ThrowIfCancellationRequested();
            return await WICHelper.GetImageSize(File.Path);
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
                var storageFile = await File.GetStorageFileAsync();

                var dataPackage = new DataPackage();
                dataPackage.SetStorageItems(new StorageFile[1] { storageFile }, false);

                RandomAccessStreamReference imageStreamRef = RandomAccessStreamReference.CreateFromFile(storageFile);
                dataPackage.Properties.Thumbnail = imageStreamRef;
                dataPackage.SetBitmap(imageStreamRef);

                Clipboard.SetContent(dataPackage);
            });
        }

        public static bool IsFileTypeSupported(string fileExt)
        {
            return fileExt == ".png" ? true : false;
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

        private Task<bool> LoadPreviewImageAsync(CancellationToken cancellationToken)
        {
            var thumbnailTCS = new TaskCompletionSource();
            return TaskExtension.RunSafe(async () =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                await Dispatcher.RunOnUiThread(async () =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var thumbnail = await ThumbnailHelper.GetThumbnailAsync(File, _png_image_size);
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
                    var sFile = await StorageFile.GetFileFromPathAsync(File.Path);

                    cancellationToken.ThrowIfCancellationRequested();
                    using (var randomAccessStream = await sFile.OpenStreamForReadAsync())
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
