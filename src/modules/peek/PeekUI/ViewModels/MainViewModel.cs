// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using PeekUI.Extensions;
using PeekUI.Helpers;
using PeekUI.Models;
using PeekUI.Native;
using WpfScreenHelper;
using Size = System.Windows.Size;

namespace PeekUI.ViewModels
{
    public class MainViewModel : ObservableObject, IDisposable
    {
        private const double ImageScale = 0.75;
        private static readonly Size MinWindowSize = new Size(720, 720);
        private static readonly Size AllowedContentGap = new Size(220, 220);

        private CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();

        private CancellationToken CancellationToken => _cancellationTokenSource.Token;

        public IntPtr ForegroundWindowHandle { get; internal set; }

        public Image ImageControl { get; set; }

        public LinkedList<string> SelectedFilePaths { get; set; } = new LinkedList<string>();

        private BitmapSource? _bitmap;

        public BitmapSource? Bitmap
        {
            get
            {
                return _bitmap;
            }

            set
            {
                if (_bitmap != value)
                {
                    _bitmap = value;
                    OnPropertyChanged(nameof(Bitmap));
                }
            }
        }

        private LinkedListNode<string>? _currentSelectedFilePath;

        public LinkedListNode<string>? CurrentSelectedFilePath
        {
            get
            {
                return _currentSelectedFilePath;
            }

            set
            {
                if (_currentSelectedFilePath != value)
                {
                    _currentSelectedFilePath = value;
                    var title = Path.GetFileName(_currentSelectedFilePath?.Value ?? string.Empty);
                    MainWindowData.Title = title;
                    OnPropertyChanged(nameof(CurrentSelectedFilePath));
                }
            }
        }

        public Visibility IsImageReady => IsLoading ? Visibility.Collapsed : Visibility.Visible;

        private bool _isLoading = true;

        public bool IsLoading
        {
            get
            {
                return _isLoading;
            }

            set
            {
                if (_isLoading != value)
                {
                    _isLoading = value;
                    OnPropertyChanged(nameof(IsLoading));
                    OnPropertyChanged(nameof(IsImageReady));
                }
            }
        }

        private ObservableWindowData _mainWindowData = new ObservableWindowData();

        public ObservableWindowData MainWindowData
        {
            get
            {
                return _mainWindowData;
            }

            set
            {
                if (_mainWindowData != value)
                {
                    _mainWindowData = value;
                    OnPropertyChanged(nameof(MainWindowData));
                }
            }
        }

        public MainViewModel(Image imageControl)
        {
            ImageControl = imageControl;
        }

        // TODO: Implement proper disposal pattern
        public void Dispose()
        {
            _cancellationTokenSource.Dispose();
            GC.SuppressFinalize(this);
        }

        public void ClearSelection()
        {
            _cancellationTokenSource.Cancel();
            _cancellationTokenSource = new CancellationTokenSource();

            CurrentSelectedFilePath = null;
            MainWindowData.Visibility = Visibility.Collapsed;
        }

        public bool TryUpdateSelectedFilePaths()
        {
            ForegroundWindowHandle = NativeMethods.GetForegroundWindow();

            // TODO: Get all neighborings files
            var selectedItems = FileExplorerHelper.GetSelectedItems(ForegroundWindowHandle);

            var isDifferentSelectedItems = !SelectedFilePaths.SequenceEqual(selectedItems);

            if (isDifferentSelectedItems)
            {
                SelectedFilePaths = new LinkedList<string>(selectedItems);
            }

            CurrentSelectedFilePath = SelectedFilePaths.First;

            return isDifferentSelectedItems;
        }

        // TODO: Implement proper cancellation pattern to support quick navigation
        public async Task RenderImageToWindowAsync(string filename)
        {
            IsLoading = true;

            var screen = Screen.FromHandle(ForegroundWindowHandle);
            Size maxWindowSize = new Size(screen.WpfBounds.Width * ImageScale, screen.WpfBounds.Height * ImageScale);

            // TODO: Support preview or thumbnail for document files
            if (FileTypeHelper.IsSupportedImage(Path.GetExtension(filename)))
            {
                await RenderSupportedImageToWindowAsync(filename, screen.Bounds, maxWindowSize);
            }
            else if (FileTypeHelper.IsMedia(Path.GetExtension(filename)) || FileTypeHelper.IsDocument(Path.GetExtension(filename)))
            {
                await RenderMediaOrDocumentToWindowAsync(filename, screen.Bounds, maxWindowSize);
            }
            else
            {
                await RenderUnsupportedFileToWindowAsync(filename, screen.Bounds, maxWindowSize);
            }
        }

        private async Task RenderSupportedImageToWindowAsync(string filename, Rect windowBounds, Size maxWindowSize)
        {
            DimensionData dimensionData = await FileLoadHelper.LoadDimensionsAsync(filename);
            if (CancellationToken.IsCancellationRequested)
            {
                _cancellationTokenSource = new CancellationTokenSource();
                return;
            }

            var windowRect = dimensionData.Size.Fit(windowBounds, maxWindowSize, MinWindowSize, AllowedContentGap, MainWindowData.TitleBarHeight);

            MainWindowData.Rectangle.Width = windowRect.Width;
            MainWindowData.Rectangle.Height = windowRect.Height;
            MainWindowData.Rectangle.Left = windowRect.Left;
            MainWindowData.Rectangle.Top = windowRect.Top;

            if (dimensionData.Size.Width > MainWindowData.Rectangle.Width || dimensionData.Size.Height > MainWindowData.Rectangle.Height)
            {
                ImageControl.StretchDirection = StretchDirection.Both;
            }
            else
            {
                ImageControl.StretchDirection = StretchDirection.DownOnly;
            }

            await LoadImageAsync(filename, ImageControl, dimensionData.Rotation, CancellationToken);
        }

        private async Task RenderMediaOrDocumentToWindowAsync(string filename, Rect windowBounds, Size maxWindowSize)
        {
            var bitmap = await FileLoadHelper.LoadThumbnailAsync(filename, true);
            if (CancellationToken.IsCancellationRequested)
            {
                _cancellationTokenSource = new CancellationTokenSource();
                return;
            }

            Bitmap = bitmap;

            var imageSize = new Size(bitmap.PixelWidth, bitmap.PixelHeight);
            var windowRect = imageSize.Fit(windowBounds, maxWindowSize, MinWindowSize, AllowedContentGap, MainWindowData.TitleBarHeight);

            MainWindowData.Rectangle.Width = windowRect.Width;
            MainWindowData.Rectangle.Height = windowRect.Height;
            MainWindowData.Rectangle.Left = windowRect.Left;
            MainWindowData.Rectangle.Top = windowRect.Top;

            MainWindowData.Visibility = Visibility.Visible;
            IsLoading = false;
        }

        private async Task RenderUnsupportedFileToWindowAsync(string filename, Rect windowBounds, Size maxWindowSize)
        {
            var contentSize = new Size(0, 0);
            var windowRect = contentSize.Fit(windowBounds, maxWindowSize, MinWindowSize, AllowedContentGap, MainWindowData.TitleBarHeight);

            MainWindowData.Rectangle.Width = windowRect.Width;
            MainWindowData.Rectangle.Height = windowRect.Height;
            MainWindowData.Rectangle.Left = windowRect.Left;
            MainWindowData.Rectangle.Top = windowRect.Top;

            var bitmap = await FileLoadHelper.LoadIconAsync(filename);
            if (CancellationToken.IsCancellationRequested)
            {
                _cancellationTokenSource = new CancellationTokenSource();
                return;
            }

            Bitmap = bitmap;
            MainWindowData.Visibility = Visibility.Visible;
            IsLoading = false;
        }

        private Task LoadImageAsync(string filename, System.Windows.Controls.Image imageControl, Rotation rotation, CancellationToken cancellationToken)
        {
            bool isFullImageLoaded = false;
            bool isThumbnailLoaded = false;
            var thumbnailLoadTask = imageControl.Dispatcher.Invoke(async () =>
            {
                var bitmap = await FileLoadHelper.LoadThumbnailAsync(filename, false);
                isThumbnailLoaded = true;

                if (CancellationToken.IsCancellationRequested)
                {
                    _cancellationTokenSource = new CancellationTokenSource();
                    return;
                }

                if (!isFullImageLoaded)
                {
                    Bitmap = bitmap;
                    MainWindowData.Visibility = Visibility.Visible;
                    IsLoading = false;
                }
            });

            var fullImageLoadTask = imageControl.Dispatcher.Invoke(async () =>
            {
                var bitmap = await FileLoadHelper.LoadFullImageAsync(filename, rotation);
                isFullImageLoaded = true;

                if (CancellationToken.IsCancellationRequested)
                {
                    _cancellationTokenSource = new CancellationTokenSource();
                    return;
                }

                Bitmap = bitmap;
                if (!isThumbnailLoaded)
                {
                    MainWindowData.Visibility = Visibility.Visible;
                    IsLoading = false;
                }
            });

            return Task.WhenAll(thumbnailLoadTask, fullImageLoadTask);
        }
    }
}
