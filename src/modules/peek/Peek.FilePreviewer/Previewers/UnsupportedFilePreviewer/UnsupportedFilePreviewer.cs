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
    using Microsoft.PowerToys.Settings.UI.Library;
    using Microsoft.UI.Dispatching;
    using Microsoft.UI.Xaml.Media.Imaging;
    using Peek.Common;
    using Peek.Common.Extensions;
    using Peek.Common.Helpers;
    using Peek.Common.Models;
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

        public UnsupportedFilePreviewer(File file)
        {
            File = file;
            FileName = file.FileName;
            DateModified = file.DateModified.ToString();
            Dispatcher = DispatcherQueue.GetForCurrentThread();
            PropertyChanged += OnPropertyChanged;

            var settingsUtils = new SettingsUtils();
            var settings = settingsUtils.GetSettingsOrDefault<PeekSettings>(PeekSettings.ModuleName);

            if (settings != null)
            {
                UnsupportedFileWidthPercent = settings.Properties.UnsupportedFileWidthPercent / 100.0;
                UnsupportedFileHeightPercent = settings.Properties.UnsupportedFileHeightPercent / 100.0;
            }
        }

        private double UnsupportedFileWidthPercent { get; set; }

        private double UnsupportedFileHeightPercent { get; set; }

        public bool IsPreviewLoaded => iconPreview != null;

        private File File { get; }

        private DispatcherQueue Dispatcher { get; }

        private Task<bool>? IconPreviewTask { get; set; }

        private Task<bool>? DisplayInfoTask { get; set; }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }

        public Task<Size> GetPreviewSizeAsync(CancellationToken cancellationToken)
        {
            return Task.Run(() =>
            {
                return new Size(UnsupportedFileWidthPercent, UnsupportedFileHeightPercent);
            });
        }

        public async Task LoadPreviewAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            State = PreviewState.Loading;

            IconPreviewTask = LoadIconPreviewAsync(cancellationToken);
            DisplayInfoTask = LoadDisplayInfoAsync(cancellationToken);

            await Task.WhenAll(IconPreviewTask, DisplayInfoTask);

            if (HasFailedLoadingPreview())
            {
                State = PreviewState.Error;
            }
        }

        public Task<bool> LoadIconPreviewAsync(CancellationToken cancellationToken)
        {
            return TaskExtension.RunSafe(async () =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                // TODO: Get icon with transparency
                IntPtr hbitmap = IntPtr.Zero;
                bool usingSystemIcon = false;
                HResult hr = IconHelper.GetIcon(Path.GetFullPath(File.Path), out hbitmap);
                if (hr != HResult.Ok)
                {
                    // Try get system icon (File icon)
                    usingSystemIcon = true;
                    IconHelper.GetSystemIcon(NativeMethods.SHSTOCKICONID.SIID_DOCNOASSOC, out hbitmap);
                }

                cancellationToken.ThrowIfCancellationRequested();
                await Dispatcher.RunOnUiThread(async () =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var iconBitmap = await GetBitmapFromHBitmapAsync(hbitmap, usingSystemIcon, cancellationToken);
                    IconPreview = iconBitmap;
                });
            });
        }

        public Task<bool> LoadDisplayInfoAsync(CancellationToken cancellationToken)
        {
            return TaskExtension.RunSafe(async () =>
            {
                // File Properties
                cancellationToken.ThrowIfCancellationRequested();
                var bytes = await PropertyHelper.GetFileSizeInBytes(File.Path);

                cancellationToken.ThrowIfCancellationRequested();
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

        // TODO: Move this to a helper file (ImagePreviewer uses the same code)
        private static async Task<BitmapSource> GetBitmapFromHBitmapAsync(IntPtr hbitmap, bool usingSystemIcon, CancellationToken cancellationToken)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                System.Drawing.Bitmap bitmap;

                if (usingSystemIcon)
                {
                    bitmap = System.Drawing.Bitmap.FromHicon(hbitmap);
                }
                else
                {
                    bitmap = System.Drawing.Image.FromHbitmap(hbitmap);
                }

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
