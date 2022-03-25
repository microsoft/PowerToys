using PeekUI.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using WpfScreenHelper;

namespace PeekUI.ViewModels
{
    public class MainViewModel : ViewModel
    {
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

        private const double ImageScale = 0.75;

        public IntPtr ForegroundWindowHandle { get; internal set; }

        private String _windowTitle = "Peek";
        public String WindowTitle
        {
            get
            {
                return _windowTitle;
            }

            set
            {
                if (_windowTitle != value)
                {
                    _windowTitle = value;
                    OnPropertyChanged(nameof(WindowTitle));
                }
            }
        }

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

        private BitmapImage _bitmap = new BitmapImage();
        public BitmapImage Bitmap
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

        public async Task RenderImageToWindowAsync(string filename, Image imageControl, double titlebarHeight)
        {
            var screen = Screen.FromHandle(ForegroundWindowHandle);

            var maxWindowWidth = screen.WpfBounds.Width * ImageScale;
            var maxWindowHeight = (screen.WpfBounds.Height * ImageScale);

            // need to take titlebar into account
            var bitmap = await LoadBitmapImageAsync(filename);
            Bitmap = bitmap;

            var imageRectangle = CalculateScaledImageRectangle(bitmap, screen, maxWindowWidth, maxWindowHeight, titlebarHeight);

            if (Bitmap.PixelWidth > maxWindowWidth || Bitmap.PixelHeight > maxWindowHeight)
            {
                imageControl.Stretch = Stretch.Uniform;
            }
            else
            {
                imageControl.Stretch = Stretch.None;
            }

            WindowWidth = imageRectangle.Width;
            WindowHeight = imageRectangle.Height + titlebarHeight;
            WindowLeft = imageRectangle.Left;
            WindowTop = imageRectangle.Top;
        }

        private static Rect CalculateScaledImageRectangle(BitmapImage bitmap, Screen screen, double maxWindowWidth, double maxWindowHeight, double titlebarHeight)
        {
            var resultingWidth = (maxWindowHeight * bitmap.PixelWidth) / bitmap.PixelHeight;
            var resultingHeight = (maxWindowWidth * bitmap.PixelHeight) / bitmap.PixelWidth;

            var renderableWindowHeight = maxWindowHeight - titlebarHeight;
            var heightScale = renderableWindowHeight / maxWindowHeight;

            // Adjust dimensions to fit inside client area
            if (resultingWidth > maxWindowWidth)
            {
                resultingWidth = maxWindowWidth * heightScale;
                resultingHeight = (resultingWidth * bitmap.PixelHeight) / bitmap.PixelWidth;

            }
            else
            {
                resultingHeight = maxWindowHeight * heightScale;
                resultingWidth = (resultingHeight * bitmap.PixelWidth) / bitmap.PixelHeight;
            }

            // Calculate offsets to center content
            double offsetX = (maxWindowWidth - resultingWidth) / 2;
            double offsetY = (maxWindowHeight - resultingHeight) / 2;

            var maxWindowLeft = screen.WpfBounds.Left + (screen.WpfBounds.Right - screen.WpfBounds.Left - maxWindowWidth) / 2;
            var maxWindowTop = screen.WpfBounds.Top + (screen.WpfBounds.Bottom - screen.WpfBounds.Top - maxWindowHeight) / 2;

            var resultingLeft = maxWindowLeft + offsetX;
            var resultingTop = maxWindowTop + offsetY;

            return new Rect(resultingLeft, resultingTop, resultingWidth, resultingHeight);
        }

        private static async Task<BitmapImage> LoadBitmapImageAsync(string filename)
        {
            return await Task.Run(() =>
            {
                var bm = new BitmapImage();
                bm.BeginInit();
                bm.UriSource = new Uri(filename);
                bm.EndInit();
                bm.Freeze();
                return bm;
            });
        }
    }
}
