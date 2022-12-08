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
    using Microsoft.UI.Xaml.Media.Imaging;
    using Peek.Common;
    using Peek.Common.Helpers;
    using Peek.FilePreviewer.Previewers.Helpers;
    using Windows.Foundation;
    using File = Peek.Common.Models.File;

    public partial class UnsupportedFilePreviewer : ObservableObject, IUnsupportedFilePreviewer, IDisposable
    {
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsPreviewLoaded))]
        private BitmapSource? iconPreview;

        [ObservableProperty]
        private string? fileName;

        [ObservableProperty]
        private string? fileType;

        [ObservableProperty]
        private string? fileSize;

        [ObservableProperty]
        private string? dateModified;

        public UnsupportedFilePreviewer(File file)
        {
            File = file;
            FileName = file.FileName;
            DateModified = file.DateModified.ToString();
            Dispatcher = DispatcherQueue.GetForCurrentThread();
        }

        public bool IsPreviewLoaded => iconPreview != null;

        private File File { get; }

        private DispatcherQueue Dispatcher { get; }

        private CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();

        private CancellationToken CancellationToken => _cancellationTokenSource.Token;

        public void Dispose()
        {
            _cancellationTokenSource.Dispose();
            GC.SuppressFinalize(this);
        }

        public Task<Size> GetPreviewSizeAsync()
        {
            return Task.Run(() =>
            {
                // TODO: This is the min size. Calculate a 20-25% of the screen.
                return new Size(680, 500);
            });
        }

        public Task LoadPreviewAsync()
        {
            var iconPreviewTask = LoadIconPreviewAsync();
            var displayInfoTask = LoadDisplayInfoAsync();

            return Task.WhenAll(iconPreviewTask, displayInfoTask);
        }

        public Task LoadIconPreviewAsync()
        {
            var iconTCS = new TaskCompletionSource();
            Dispatcher.TryEnqueue(async () =>
            {
                if (CancellationToken.IsCancellationRequested)
                {
                    _cancellationTokenSource = new CancellationTokenSource();
                    return;
                }

                // TODO: Get icon with transparency
                IconHelper.GetIcon(Path.GetFullPath(File.Path), out IntPtr hbitmap);
                var iconBitmap = await GetBitmapFromHBitmapAsync(hbitmap);
                IconPreview = iconBitmap;

                iconTCS.SetResult();
            });

            return iconTCS.Task;
        }

        public Task LoadDisplayInfoAsync()
        {
            var displayInfoTCS = new TaskCompletionSource();
            Dispatcher.TryEnqueue(async () =>
            {
                if (CancellationToken.IsCancellationRequested)
                {
                    _cancellationTokenSource = new CancellationTokenSource();
                    return;
                }

                // File Properties
                var bytes = await PropertyHelper.GetFileSizeInBytes(File.Path);
                FileSize = ReadableStringHelper.BytesToReadableString(bytes);

                var type = await PropertyHelper.GetFileType(File.Path);
                FileType = type;

                displayInfoTCS.SetResult();
            });

            return displayInfoTCS.Task;
        }

        // TODO: Move this to a helper file (ImagePrevier uses the same code)
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
