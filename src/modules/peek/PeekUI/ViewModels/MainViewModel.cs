using PeekUI.Helpers;
using PeekUI.Models;
using System;
using System.Collections.Generic;
using System.Drawing.Imaging;
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
        private CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();

        private CancellationToken CancellationToken => _cancellationTokenSource.Token;

        public IntPtr ForegroundWindowHandle { get; internal set; }

        public Image? ImageControl { get; set; }

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

        public void ClearSelection()
        {
            _cancellationTokenSource.Cancel();
            _cancellationTokenSource = new CancellationTokenSource();

            CurrentSelectedFilePath = null;
            MainWindowData.Visibility = Visibility.Collapsed;
        }

        public bool TryUpdateSelectedFilePaths()
        {
            ForegroundWindowHandle = FileExplorerHelper.GetForegroundWindow();
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
                await RenderSupportedImageToWindowAsync(filename, screen, maxWindowSize);
            }
            else if (FileTypeHelper.IsMedia(Path.GetExtension(filename)) || FileTypeHelper.IsDocument(Path.GetExtension(filename)))
            {
                await RenderMediaOrDocumentToWindowAsync(filename, screen, maxWindowSize);
            }
            else
            {
                await RenderUnsupportedFileToWindowAsync(filename, screen, maxWindowSize);
            }
        }

        private async Task RenderSupportedImageToWindowAsync(string filename, Screen screen, Size maxWindowSize)
        {
            DimensionData dimensionData = await LoadDimensionsAsync(filename);
            var windowRect = CalculateScaledImageRectangle(dimensionData.Size!.Value, screen, maxWindowSize, MainWindowData.TitleBarHeight);
            MainWindowData.Rectangle.Width = windowRect.Width;
            MainWindowData.Rectangle.Height = windowRect.Height;
            MainWindowData.Rectangle.Left = windowRect.Left;
            MainWindowData.Rectangle.Top = windowRect.Top;

            if (dimensionData.Size!.Value.Width > MainWindowData.Rectangle.Width || dimensionData.Size!.Value.Height > MainWindowData.Rectangle.Height)
            {
                if (ImageControl != null)
                {
                    ImageControl.StretchDirection = StretchDirection.Both;
                }
            }
            else
            {
                if (ImageControl != null)
                {
                    ImageControl.StretchDirection = StretchDirection.DownOnly;
                }
            }


            await LoadImageAsync(filename, ImageControl, dimensionData.Rotation, CancellationToken);
        }

        private async Task RenderMediaOrDocumentToWindowAsync(string filename, Screen screen, Size maxWindowSize)
        {
            var bitmap = await LoadThumbnailAsync(filename, true);
            if (CancellationToken.IsCancellationRequested)
            {
                return;
            }
            _cancellationTokenSource = new CancellationTokenSource();

            Bitmap = bitmap;

            var windowRect = CalculateScaledImageRectangle(new Size(bitmap.PixelWidth, bitmap.PixelHeight), screen, maxWindowSize, MainWindowData.TitleBarHeight);
            MainWindowData.Rectangle.Width = windowRect.Width;
            MainWindowData.Rectangle.Height = windowRect.Height;
            MainWindowData.Rectangle.Left = windowRect.Left;
            MainWindowData.Rectangle.Top = windowRect.Top;

            MainWindowData.Visibility = Visibility.Visible;
        }

        private async Task RenderUnsupportedFileToWindowAsync(string filename, Screen screen, Size maxWindowSize)
        {
            var windowRect = CalculateScaledImageRectangle(new Size(0, 0), screen, maxWindowSize, MainWindowData.TitleBarHeight);
            MainWindowData.Rectangle.Width = windowRect.Width;
            MainWindowData.Rectangle.Height = windowRect.Height;
            MainWindowData.Rectangle.Left = windowRect.Left;
            MainWindowData.Rectangle.Top = windowRect.Top;

            var bitmap = await LoadIconAsync(filename);
            if (CancellationToken.IsCancellationRequested)
            {
                return;
            }
            _cancellationTokenSource = new CancellationTokenSource();

            Bitmap = bitmap;
            MainWindowData.Visibility = Visibility.Visible;
        }

        private async Task LoadImageAsync(string filename, System.Windows.Controls.Image? imageControl, Rotation rotation, CancellationToken cancellationToken)
        {
            if (imageControl == null)
            {
                return;
            }

            bool isFullImageLoaded = false;
            bool isThumbnailLoaded = false;
            var thumbnailLoadTask = imageControl.Dispatcher.Invoke(async () =>
            {
                var bitmap = await LoadThumbnailAsync(filename, false);
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
                var bitmap = await LoadFullImageAsync(filename, rotation);
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

        private static Task<DimensionData> LoadDimensionsAsync(string filename)
        {
            return Task.Run(() =>
            {
                Size? size = null;
                try
                {
                    using (FileStream stream = File.OpenRead(filename))
                    {
                        string extension = Path.GetExtension(stream.Name);
                        if (FileTypeHelper.IsSupportedImage(extension))
                        {
                            using (System.Drawing.Image sourceImage = System.Drawing.Image.FromStream(stream, false, false))
                            {
                                var rotation = EvaluateRotationToApply(sourceImage);
                                if (rotation == Rotation.Rotate90 || rotation == Rotation.Rotate270)
                                {
                                    size = new Size(sourceImage.Height, sourceImage.Width);
                                }
                                else
                                {
                                    size = new Size(sourceImage.Width, sourceImage.Height);
                                }

                                return Task.FromResult(new DimensionData { Size = size, Rotation = rotation });
                            }
                        }
                        else
                        {
                            return Task.FromResult(new DimensionData { Size = size, Rotation = Rotation.Rotate0 });
                        }

                    }
                }
                catch (Exception)
                {
                    return Task.FromResult(new DimensionData { Size = size, Rotation = Rotation.Rotate0 });
                }
            });
        }

        private static Rect CalculateScaledImageRectangle(Size imageDimensions, Screen screen, Size maxWindowSize, double titleBarHeight)
        {
            double resultingWidth = imageDimensions.Width;
            double resultingHeight = imageDimensions.Height;

            var ratioWidth = imageDimensions.Width / maxWindowSize.Width;
            var ratioHeight = imageDimensions.Height / maxWindowSize.Height;

            if (ratioWidth > ratioHeight)
            {
                if (ratioWidth > 1)
                {
                    resultingWidth = maxWindowSize.Width;
                    resultingHeight = imageDimensions.Height / ratioWidth;
                }
            }
            else
            {
                if (ratioHeight > 1)
                {
                    resultingWidth = imageDimensions.Width / ratioHeight;
                    resultingHeight = maxWindowSize.Height;
                }
            }

            double minSize = 720;
            if (resultingWidth < minSize - 220)
            {
                resultingWidth = minSize;
            }

            if (resultingHeight < minSize - 220)
            {
                resultingHeight = minSize;
            }

            resultingHeight += titleBarHeight;

            // Calculate offsets to center content
            double offsetX = (maxWindowSize.Width - resultingWidth) / 2;
            double offsetY = (maxWindowSize.Height - resultingHeight) / 2;

            var maxWindowLeft = screen.WpfBounds.Left + (screen.WpfBounds.Right - screen.WpfBounds.Left - maxWindowSize.Width) / 2;
            var maxWindowTop = screen.WpfBounds.Top + (screen.WpfBounds.Bottom - screen.WpfBounds.Top - maxWindowSize.Height) / 2;

            var resultingLeft = maxWindowLeft + offsetX;
            var resultingTop = maxWindowTop + offsetY;

            return new Rect(resultingLeft, resultingTop, resultingWidth, resultingHeight);
        }

        private static async Task<BitmapSource> LoadThumbnailAsync(string filename, bool iconFallback)
        {
            var thumbnail =  await Task.Run(() =>
            {
                var bitmapSource = WindowsThumbnailProvider.GetThumbnail(filename, iconFallback);
                bitmapSource.Freeze();
                return bitmapSource;
            });

            return thumbnail;

        }

        private static Task<BitmapSource> LoadIconAsync(string filename)
        {
            return Task.Run(() =>
            {
                var bitmapSource = WindowsThumbnailProvider.GetIcon(filename);
                bitmapSource.Freeze();
                return bitmapSource;
            });
        }


        private static Task<BitmapImage> LoadFullImageAsync(string filename, Rotation rotation)
        {
            return Task.Run(() =>
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.UriSource = new Uri(filename);
                bitmap.Rotation = rotation;
                bitmap.EndInit();
                bitmap.Freeze();
                return bitmap;
            });
        }

        private static Rotation EvaluateRotationToApply(System.Drawing.Image image)
        {
            PropertyItem? property = image.PropertyItems?.FirstOrDefault(p => p.Id == 274);

            if (property != null && property.Value != null && property.Value.Length > 0)
            {
                int orientation = property.Value[0];

                if (orientation == 6)
                {
                    return Rotation.Rotate90;
                }

                if (orientation == 3)
                {
                    return Rotation.Rotate180;
                }

                if (orientation == 8)
                {
                    return Rotation.Rotate270;
                }
            }

            return Rotation.Rotate0;
        }

        public void Dispose()
        {
            _cancellationTokenSource.Dispose();
            GC.SuppressFinalize(this);
        }
    }

    internal class DimensionData
    {
        public Size? Size { get; set; }
        public Rotation Rotation { get; set; }
    }
}
