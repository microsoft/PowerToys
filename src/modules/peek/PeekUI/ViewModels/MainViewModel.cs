using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace PeekUI.ViewModels
{
    public class MainViewModel : ViewModel
    {
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

            Dispatcher.CurrentDispatcher.Invoke(() => Bitmap = bitmap);
        }
    }
}
