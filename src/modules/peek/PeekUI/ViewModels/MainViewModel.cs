using PeekUI.Helpers;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using WpfScreenHelper;
using Size = System.Windows.Size;

namespace PeekUI.ViewModels
{
    public class MainViewModel : ViewModel
    {
        private const double ImageScale = 0.75;
        private bool _dimensionFound;

        public LinkedList<string> SelectedFilePaths { get; set; } = new LinkedList<string>();

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
                    OnPropertyChanged(nameof(CurrentSelectedFilePath));
                }
            }
        }

        public IntPtr ForegroundWindowHandle { get; internal set; }

        private double _windowLeft;
        public double WindowLeft
        {
            get
            {
                return _windowLeft;
            }

            set
            {
                if (_windowLeft != value)
                {
                    _windowLeft = value;
                    OnPropertyChanged(nameof(WindowLeft));
                }
            }
        }



        private double _windowTop;
        public double WindowTop
        {
            get
            {
                return _windowTop;
            }

            set
            {
                if (_windowTop != value)
                {
                    _windowTop = value;
                    OnPropertyChanged(nameof(WindowTop));
                }
            }
        }

        private double _windowHeight;
        public double WindowHeight
        {
            get
            {
                return _windowHeight;
            }

            set
            {
                if (_windowHeight != value)
                {
                    _windowHeight = value;
                    OnPropertyChanged(nameof(WindowHeight));
                }
            }
        }

        private double _windowWidth;
        public double WindowWidth
        {
            get
            {
                return _windowWidth;
            }

            set
            {
                if (_windowWidth != value)
                {
                    _windowWidth = value;
                    OnPropertyChanged(nameof(WindowWidth));
                }
            }
        }

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

        private Visibility _visibility;

        public Visibility MainWindowVisibility
        {
            get
            {
                return _visibility;
            }

            set
            {
                if (_visibility != value)
                {
                    _visibility = value;

                    OnPropertyChanged(nameof(MainWindowVisibility));
                }
            }
        }

        public void ClearSelection()
        {
            SelectedFilePaths.Clear();
            CurrentSelectedFilePath = null;
        }

        public bool TryUpdateSelectedFilePaths()
        {
            ForegroundWindowHandle = FileExplorerHelper.GetForegroundWindow();
            var selectedItems = FileExplorerHelper.GetSelectedItems(ForegroundWindowHandle);

            var isDifferentSelectedItems = !SelectedFilePaths.SequenceEqual(selectedItems);

            if (isDifferentSelectedItems)
            {
                SelectedFilePaths = new LinkedList<string>(selectedItems);
                CurrentSelectedFilePath = SelectedFilePaths.First;
            }

            return isDifferentSelectedItems;
        }

        public async Task RenderImageToWindowAsync(string filename, System.Windows.Controls.Image imageControl)
        {
            var screen = Screen.FromHandle(ForegroundWindowHandle);
            Size maxWindowSize = new Size(screen.WpfBounds.Width * ImageScale, screen.WpfBounds.Height* ImageScale);

            // if IsSupportedImage()
            //      then load dimensions
            //      then resize window
            //      then load thumbnail-only + load full image in parallel
            //      Only stretch image control if dimensions > window size
            // else if IsMedia() || IsDocument()
            //      then load thumbnail (fallback icon)
            //      then resize window
            // else
            //      then resize window
            //      load icon (fallback error)


            BitmapSource bitmap;
            Size? dimensions = await LoadDimensionsAsync(filename);
            _dimensionFound = dimensions.HasValue;

            if (_dimensionFound)
            {
                var windowRect = CalculateScaledImageRectangle(dimensions.Value, screen, maxWindowSize);

                WindowWidth = windowRect.Width;
                WindowHeight = windowRect.Height;
                WindowLeft = windowRect.Left;
                WindowTop = windowRect.Top;

                // todo: load full image as well
                await LoadThumbnailAsync(filename, imageControl);
            }
            else
            {
                bitmap = await LoadThumbnailAsync(filename, imageControl);

                var windowRect = CalculateScaledImageRectangle(new Size(bitmap.PixelWidth, bitmap.PixelHeight), screen, maxWindowSize);

                WindowWidth = windowRect.Width;
                WindowHeight = windowRect.Height;
                WindowLeft = windowRect.Left;
                WindowTop = windowRect.Top;
            }
        }

        private static Task<Size?> LoadDimensionsAsync(string filename)
        {
            return Task.Run(() =>
            {
                Size? size = null;
                try
                {
                    using (FileStream stream = File.OpenRead(filename))
                    {
                        string extension = Path.GetExtension(stream.Name);
                        if (IsSupportedFileType(extension))
                        {
                            using (System.Drawing.Image sourceImage = System.Drawing.Image.FromStream(stream, false, false))
                            {
                                if (IsDimensionFlipped(sourceImage))
                                {
                                    size = new Size(sourceImage.Height, sourceImage.Width);
                                }
                                else
                                {
                                    size = new Size(sourceImage.Width, sourceImage.Height);
                                }

                                return Task.FromResult(size);
                            }
                        }
                        else
                        {
                            return Task.FromResult(size);
                        }

                    }
                }
                catch (Exception)
                {
                    return Task.FromResult(size);
                }
            });
        }

        private static bool IsSupportedFileType(string extension) => extension switch
        {
            ".bmp" => true,
            ".gif" => true,
            ".jpg" => true,
            ".jfif" => true,
            ".jfi" => true,
            ".jif" => true,
            ".jpeg" => true,
            ".jpe" => true,
            ".png" => true,
            ".tif" => true,
            ".tiff" => true,
            _ => false,
        };

        private static Rect CalculateScaledImageRectangle(Size imageDimensions, Screen screen, Size maxWindowSize)
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

            // Calculate offsets to center content
            double offsetX = (maxWindowSize.Width - resultingWidth) / 2;
            double offsetY = (maxWindowSize.Height - resultingHeight) / 2;

            var maxWindowLeft = screen.WpfBounds.Left + (screen.WpfBounds.Right - screen.WpfBounds.Left - maxWindowSize.Width) / 2;
            var maxWindowTop = screen.WpfBounds.Top + (screen.WpfBounds.Bottom - screen.WpfBounds.Top - maxWindowSize.Height) / 2;

            var resultingLeft = maxWindowLeft + offsetX;
            var resultingTop = maxWindowTop + offsetY;

            return new Rect(resultingLeft, resultingTop, resultingWidth, resultingHeight);
        }

        private async Task<BitmapSource> LoadThumbnailAsync(string filename, System.Windows.Controls.Image imageControl)
        {

            var thumbnail =  await Task.Run(() =>
            {
                var bitmapSource = WindowsThumbnailProvider.GetThumbnail(filename);
                bitmapSource.Freeze();
                Bitmap = bitmapSource;
                return bitmapSource;
            });

            return thumbnail;

        }

        private async Task LoadFullImageAsync(string filename)
        {
            await Task.Run(() =>
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(filename);
                bitmap.EndInit();
                bitmap.Freeze();
                Bitmap = bitmap;
            });
        }

        public static bool IsDimensionFlipped(System.Drawing.Image image)
        {
            PropertyItem? property = image.PropertyItems?.FirstOrDefault(p => p.Id == 274);

            if (property != null && property.Value != null && property.Value.Length > 0)
            {
                int orientation = property.Value[0];
                if (orientation == 6 || orientation == 8)
                    return true;
            }

            return false;
        }
    }
}
