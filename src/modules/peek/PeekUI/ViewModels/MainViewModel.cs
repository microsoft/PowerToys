using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace PeekUI.ViewModels
{
    public class MainViewModel : ViewModel
    {
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

        private BitmapImage? _bitmap;
        public BitmapImage? Bitmap
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

        public async Task LoadImageAsync(string filename)
        {
            var bitmap = await Task.Run(() =>
            {
                var bm = new BitmapImage();
                bm.BeginInit();
                bm.UriSource = new Uri(filename);
                bm.EndInit();
                bm.Freeze();
                return bm;
            });

            Dispatcher.CurrentDispatcher.Invoke(() => SetBitmapImage(bitmap));
        }

        private void SetBitmapImage(BitmapImage bitmapImage)
        {
            Bitmap = bitmapImage;
        }
    }
}
