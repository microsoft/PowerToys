// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Peek.FilePreviewer.Previewers
{
    using System;
    using System.Drawing;
    using System.Threading;
    using System.Threading.Tasks;
    using CommunityToolkit.Mvvm.ComponentModel;
    using Microsoft.UI.Dispatching;
    using Microsoft.UI.Xaml.Media.Imaging;
    using Peek.Common.Models;
    using Windows.Storage.FileProperties;

    [INotifyPropertyChanged]
    public partial class ImagePreviewer : IDisposable
    {
        [ObservableProperty]
        private BitmapImage? preview;

        public ImagePreviewer(File file)
        {
            File = file;
            Dispatcher = DispatcherQueue.GetForCurrentThread();
        }

        private File File { get; }

        private DispatcherQueue Dispatcher { get; }

        private CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();

        private CancellationToken CancellationToken => _cancellationTokenSource.Token;

        public Task<Size> GetPreviewSizeAsync()
        {
            return Task.FromResult(new Size(1280, 720));
        }

        public Task LoadPreviewAsync()
        {
            bool isFullImageLoaded = false;

            var thumbnailTCS = new TaskCompletionSource();
            Dispatcher.TryEnqueue(async () =>
            {
                // TODO: Improve this is slower getting the full image.
                var storageFile = await File.GetStorageFileAsync();
                var thumbnail = await storageFile.GetThumbnailAsync(ThumbnailMode.SingleItem, 1280, ThumbnailOptions.None);

                if (CancellationToken.IsCancellationRequested)
                {
                    _cancellationTokenSource = new CancellationTokenSource();
                    return;
                }

                if (!isFullImageLoaded)
                {
                    var bitmap = new BitmapImage();
                    await bitmap.SetSourceAsync(thumbnail);
                    Preview = bitmap;
                }

                thumbnailTCS.SetResult();
            });

            var fullImageTCS = new TaskCompletionSource();
            Dispatcher.TryEnqueue(() =>
            {
                // TODO: Check if this is performant
                var bitmap = new BitmapImage();
                bitmap.UriSource = new Uri(File.Path);

                isFullImageLoaded = true;

                if (CancellationToken.IsCancellationRequested)
                {
                    _cancellationTokenSource = new CancellationTokenSource();
                    return;
                }

                Preview = bitmap;

                fullImageTCS.SetResult();
            });

            return Task.WhenAll(thumbnailTCS.Task, fullImageTCS.Task);
        }

        public void Dispose()
        {
            _cancellationTokenSource.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
