// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Peek.Common.Extensions;
using Peek.Common.Helpers;
using Peek.Common.Models;
using Windows.Foundation;

namespace Peek.FilePreviewer.Previewers
{
    public partial class SvgPreviewer : ObservableObject, ISvgPreviewer, IDisposable
    {
        [ObservableProperty]
        private ImageSource? preview;

        [ObservableProperty]
        private PreviewState state;

        [ObservableProperty]
        private Size imageSize;

        [ObservableProperty]
        private double scalingFactor;

        public SvgPreviewer(IFileSystemItem file)
        {
            Item = file;
            Dispatcher = DispatcherQueue.GetForCurrentThread();
        }

        private IFileSystemItem Item { get; }

        private DispatcherQueue Dispatcher { get; }

        private Task<bool>? LowQualityThumbnailTask { get; set; }

        private Task<bool>? FullQualityImageTask { get; set; }

        private bool IsFullImageLoaded => FullQualityImageTask?.Status == TaskStatus.RanToCompletion;

        public static bool IsFileTypeSupported(string fileExt)
        {
            return fileExt == ".svg";
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }

        public async Task<Size?> GetPreviewSizeAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var size = await Task.Run(Item.GetSvgSize);
            if (size != null)
            {
                ImageSize = size.Value;
            }

            return ImageSize;
        }

        public async Task LoadPreviewAsync(CancellationToken cancellationToken)
        {
            State = PreviewState.Loading;

            LowQualityThumbnailTask = LoadLowQualityThumbnailAsync(cancellationToken);
            FullQualityImageTask = LoadFullQualityImageAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            await Task.WhenAll(LowQualityThumbnailTask, FullQualityImageTask);

            if (Preview == null && HasFailedLoadingPreview())
            {
                State = PreviewState.Error;
            }
        }

        public async Task CopyAsync()
        {
            await Dispatcher.RunOnUiThread(async () =>
            {
                var storageItem = await Item.GetStorageItemAsync();
                ClipboardHelper.SaveToClipboard(storageItem);
            });
        }

        partial void OnPreviewChanged(ImageSource? value)
        {
            if (Preview != null)
            {
                State = PreviewState.Loaded;
            }
        }

        private Task<bool> LoadLowQualityThumbnailAsync(CancellationToken cancellationToken)
        {
            return TaskExtension.RunSafe(async () =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                // TODO: get thumbnail
                cancellationToken.ThrowIfCancellationRequested();

                /*await Dispatcher.RunOnUiThread(async () =>
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    // TODO: update preview
                });*/

                await Task.CompletedTask;
            });
        }

        private Task<bool> LoadFullQualityImageAsync(CancellationToken cancellationToken)
        {
            return TaskExtension.RunSafe(async () =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                using FileStream stream = File.OpenRead(Item.Path);
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    await Dispatcher.RunOnUiThread(async () =>
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        var source = new SvgImageSource();
                        source.RasterizePixelHeight = ImageSize.Height;
                        source.RasterizePixelWidth = ImageSize.Width;

                        var loadStatus = await source.SetSourceAsync(stream.AsRandomAccessStream());
                        if (loadStatus != SvgImageSourceLoadStatus.Success)
                        {
                            Debug.WriteLine("Error loading SVG: " + loadStatus.ToString());
                            throw new ArgumentNullException(nameof(source));
                        }

                        Preview = source;
                    });
                }
            });
        }

        private bool HasFailedLoadingPreview()
        {
            var hasFailedLoadingLowQualityThumbnail = !(LowQualityThumbnailTask?.Result ?? true);
            var hasFailedLoadingFullQualityImage = !(FullQualityImageTask?.Result ?? true);

            return hasFailedLoadingLowQualityThumbnail && hasFailedLoadingFullQualityImage;
        }
    }
}
