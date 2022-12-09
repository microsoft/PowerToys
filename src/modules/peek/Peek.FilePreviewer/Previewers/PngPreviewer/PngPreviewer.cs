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
    using Peek.Common.Extensions;
    using Windows.ApplicationModel.DataTransfer;
    using Windows.Foundation;
    using Windows.Graphics.Imaging;
    using Windows.Storage;
    using Windows.Storage.Streams;
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

        public async Task LoadPreviewAsync()
        {
            State = PreviewState.Loading;

            var previewTask = LoadPreviewImageAsync();

            await Task.WhenAll(previewTask);

            if (Preview == null)
            {
                State = PreviewState.Error;
            }
        }

        public async Task CopyAsync()
        {
            await Dispatcher.RunOnUiThread(async () =>
            {
                var storageFile = await StorageFile.GetFileFromPathAsync(File.Path);

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

        private Task LoadPreviewImageAsync()
        {
            var thumbnailTCS = new TaskCompletionSource();
            Dispatcher.TryEnqueue(async () =>
            {
                if (CancellationToken.IsCancellationRequested)
                {
                    _cancellationTokenSource = new CancellationTokenSource();
                    return;
                }

                Preview = await ThumbnailHelper.GetThumbnailAsync(File.Path, _png_image_size);

                thumbnailTCS.SetResult();
            });

            return thumbnailTCS.Task;
        }
    }
}
