using PeekUI.Extensions;
using PeekUI.Helpers;
using PeekUI.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using WpfScreenHelper;
using Size = System.Windows.Size;

namespace PeekUI.ViewModels
{
    public class MainViewModel : ObservableObject, IDisposable
    {
        private const double ImageScale = 0.75;
        private readonly Size MinWindowSize = new Size(720, 720);
        private readonly Size AllowedContentGap = new Size(220, 220);

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
            var selectedItems = FileExplorerHelper.GetSelectedItems(ForegroundWindowHandle);

            var isDifferentSelectedItems = !SelectedFilePaths.SequenceEqual(selectedItems);

            if (isDifferentSelectedItems)
            {
                SelectedFilePaths = new LinkedList<string>(selectedItems);
            }

            CurrentSelectedFilePath = SelectedFilePaths.First;

            return isDifferentSelectedItems;
        }

        public async Task RenderImageToWindowAsync(string filename)
        {
            var screen = Screen.FromHandle(ForegroundWindowHandle);
            Size maxWindowSize = new Size(screen.WpfBounds.Width * ImageScale, (screen.WpfBounds.Height) * ImageScale);

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
                return;
            }
            _cancellationTokenSource = new CancellationTokenSource();

            Bitmap = bitmap;

            var imageSize = new Size(bitmap.PixelWidth, bitmap.PixelHeight);
            var windowRect = imageSize.Fit(windowBounds, maxWindowSize, MinWindowSize, AllowedContentGap, MainWindowData.TitleBarHeight);

            MainWindowData.Rectangle.Width = windowRect.Width;
            MainWindowData.Rectangle.Height = windowRect.Height;
            MainWindowData.Rectangle.Left = windowRect.Left;
            MainWindowData.Rectangle.Top = windowRect.Top;

            MainWindowData.Visibility = Visibility.Visible;
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
                return;
            }
            _cancellationTokenSource = new CancellationTokenSource();

            Bitmap = bitmap;
            MainWindowData.Visibility = Visibility.Visible;
        }

        private async Task LoadImageAsync(string filename, System.Windows.Controls.Image imageControl, Rotation rotation, CancellationToken cancellationToken)
        {
            bool isFullImageLoaded = false;
            bool isThumbnailLoaded = false;
            var thumbnailLoadTask = imageControl.Dispatcher.Invoke(async () =>
            {
                var bitmap = await FileLoadHelper.LoadThumbnailAsync(filename, false);
                isThumbnailLoaded = true;

                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                if (!isFullImageLoaded)
                {
                    Bitmap = bitmap;
                    MainWindowData.Visibility = Visibility.Visible;
                }
            });

            var fullImageLoadTask = imageControl.Dispatcher.Invoke(async () =>
            {
                var bitmap = await FileLoadHelper.LoadFullImageAsync(filename, rotation);
                isFullImageLoaded = true;

                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                Bitmap = bitmap;
                if (!isThumbnailLoaded)
                {
                    MainWindowData.Visibility = Visibility.Visible;
                }
            });

            await Task.WhenAll(thumbnailLoadTask, fullImageLoadTask);
            _cancellationTokenSource = new CancellationTokenSource();
        }
    }
}