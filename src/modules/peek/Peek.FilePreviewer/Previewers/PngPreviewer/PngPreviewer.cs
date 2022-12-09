// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Peek.FilePreviewer.Previewers
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using CommunityToolkit.Mvvm.ComponentModel;
    using Microsoft.UI.Dispatching;
    using Microsoft.UI.Xaml.Media.Imaging;
    using Peek.Common.Extensions;
    using Windows.Foundation;
    using Windows.Graphics.Imaging;
    using Windows.Storage;
    using File = Peek.Common.Models.File;

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

        private CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();

        private CancellationToken CancellationToken => _cancellationTokenSource.Token;

        private bool IsFullImageLoaded => FullQualityImageTask?.Status == TaskStatus.RanToCompletion;

        public void Dispose()
        {
            _cancellationTokenSource.Dispose();
            GC.SuppressFinalize(this);
        }

        public async Task<Size> GetPreviewSizeAsync(CancellationToken cancellationToken)
        {
            var propertyImageSize = await PropertyHelper.GetImageSize(File.Path);
            if (propertyImageSize != Size.Empty)
            {
                return propertyImageSize;
            }

            return await WICHelper.GetImageSize(File.Path);
        }

        public async Task LoadPreviewAsync(CancellationToken cancellationToken)
        {
            State = PreviewState.Loading;

            PreviewQualityThumbnailTask = LoadPreviewImageAsync();
            FullQualityImageTask = LoadFullImageAsync();

            await Task.WhenAll(PreviewQualityThumbnailTask, FullQualityImageTask);

            if (Preview == null)
            {
                State = PreviewState.Error;
            }
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

        private Task<bool> LoadPreviewImageAsync()
        {
            var thumbnailTCS = new TaskCompletionSource();
            return TaskExtension.RunSafe(async () =>
            {
                if (CancellationToken.IsCancellationRequested)
                {
                    _cancellationTokenSource = new CancellationTokenSource();
                    return;
                }

                if (!IsFullImageLoaded)
                {
                    await Dispatcher.RunOnUiThread(async () =>
                    {
                        Preview = await ThumbnailHelper.GetThumbnailAsync(File.Path, _png_image_size);
                    });
                }
            });
        }

        private Task<bool> LoadFullImageAsync()
        {
            var thumbnailTCS = new TaskCompletionSource();
            return TaskExtension.RunSafe(async () =>
            {
                if (CancellationToken.IsCancellationRequested)
                {
                    _cancellationTokenSource = new CancellationTokenSource();
                    return;
                }

                await Dispatcher.RunOnUiThread(async () =>
                {
                    WriteableBitmap? bitmap = null;

                    var sFile = await StorageFile.GetFileFromPathAsync(File.Path);
                    using (var randomAccessStream = await sFile.OpenStreamForReadAsync())
                    {
                        // Create an encoder with the desired format
                        var decoder = await BitmapDecoder.CreateAsync(
                            BitmapDecoder.PngDecoderId,
                            randomAccessStream.AsRandomAccessStream());

                        var softwareBitmap = await decoder.GetSoftwareBitmapAsync(
                            BitmapPixelFormat.Bgra8,
                            BitmapAlphaMode.Premultiplied);

                        // full quality image
                        bitmap = new WriteableBitmap((int)decoder.PixelWidth, (int)decoder.PixelHeight);
                        softwareBitmap?.CopyToBuffer(bitmap.PixelBuffer);
                    }

                    Preview = bitmap;
                });
            });
        }
    }
}
