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
    using Peek.Common.Helpers;
    using Peek.FilePreviewer.Previewers.Helpers;
    using Windows.Foundation;
    using File = Peek.Common.Models.File;

    public partial class UnsupportedFilePreviewer : ObservableObject, IUnsupportedFilePreviewer, IDisposable
    {
        [ObservableProperty]
        private BitmapSource? iconPreview;

        [ObservableProperty]
        private string? fileName;

        [ObservableProperty]
        private string? fileType;

        [ObservableProperty]
        private string? fileSize;

        [ObservableProperty]
        private string? dateModified;

        [ObservableProperty]
        private PreviewState state;

        public UnsupportedFilePreviewer(File file, CancellationToken cancellationToken)
        {
            File = file;
            CancellationToken = cancellationToken;
            FileName = file.FileName;
            DateModified = file.DateModified.ToString();
            Dispatcher = DispatcherQueue.GetForCurrentThread();

            PropertyChanged += OnPropertyChanged;
        }

        private File File { get; }

        private CancellationToken CancellationToken { get; }

        private DispatcherQueue Dispatcher { get; }

        private Task<bool>? IconPreviewTask { get; set; }

        private Task<bool>? DisplayInfoTask { get; set; }

        public void Dispose()
        {
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

        public async Task LoadPreviewAsync()
        {
            CancellationToken.ThrowIfCancellationRequested();

            State = PreviewState.Loading;

            IconPreviewTask = LoadIconPreviewAsync();
            DisplayInfoTask = LoadDisplayInfoAsync();

            await Task.WhenAll(IconPreviewTask, DisplayInfoTask);

            if (HasFailedLoadingPreview())
            {
                State = PreviewState.Error;
            }
        }

        public Task<bool> LoadIconPreviewAsync()
        {
            return TaskExtension.RunSafe(async () =>
            {
                CancellationToken.ThrowIfCancellationRequested();

                // TODO: Get icon with transparency
                IconHelper.GetIcon(Path.GetFullPath(File.Path), out IntPtr hbitmap);

                CancellationToken.ThrowIfCancellationRequested();

                await Dispatcher.RunOnUiThread(async () =>
                {
                    CancellationToken.ThrowIfCancellationRequested();
                    var iconBitmap = await GetBitmapFromHBitmapAsync(hbitmap, CancellationToken);
                    IconPreview = iconBitmap;
                });
            });
        }

        public Task<bool> LoadDisplayInfoAsync()
        {
            return TaskExtension.RunSafe(async () =>
            {
                // File Properties
                CancellationToken.ThrowIfCancellationRequested();
                var bytes = await PropertyHelper.GetFileSizeInBytes(File.Path);

                CancellationToken.ThrowIfCancellationRequested();
                var type = await PropertyHelper.GetFileType(File.Path);

                await Dispatcher.RunOnUiThread(() =>
                {
                    FileSize = ReadableStringHelper.BytesToReadableString(bytes);
                    FileType = type;
                    return Task.CompletedTask;
                });
            });
        }

        private void OnPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(IconPreview))
            {
                if (IconPreview != null)
                {
                    State = PreviewState.Loaded;
                }
            }
        }

        private bool HasFailedLoadingPreview()
        {
            var hasFailedLoadingIconPreview = !(IconPreviewTask?.Result ?? true);
            var hasFailedLoadingDisplayInfo = !(DisplayInfoTask?.Result ?? true);

            return hasFailedLoadingIconPreview && hasFailedLoadingDisplayInfo;
        }

        // TODO: Move this to a helper file (ImagePrevier uses the same code)
        private static async Task<BitmapSource> GetBitmapFromHBitmapAsync(IntPtr hbitmap, CancellationToken cancellationToken)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
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
    }
}
