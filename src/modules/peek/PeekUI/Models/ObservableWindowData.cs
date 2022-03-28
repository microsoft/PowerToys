using System.Windows;

namespace PeekUI.Models
{
    public class ObservableWindowData : ObservableObject
    {
        private double _titleBarHeight;

        public double TitleBarHeight
        {
            get
            {
                return _titleBarHeight;
            }

            set
            {
                if (_titleBarHeight != value)
                {
                    _titleBarHeight = value;
                    OnPropertyChanged(nameof(TitleBarHeight));
                }
            }
        }

        private ObservableRectangle _rectangle = new ObservableRectangle();

        public ObservableRectangle Rectangle
        {
            get
            {
                return _rectangle;
            }

            set
            {
                if (_rectangle != value)
                {
                    _rectangle = value;
                    OnPropertyChanged(nameof(Rectangle));
                }
            }
        }

        private string _title = string.Empty;

        public string Title
        {
            get
            {
                return _title;
            }

            set
            {
                if (_title != value)
                {
                    _title = value;
                    OnPropertyChanged(nameof(Title));
                }
            }
        }

        private Visibility _visibility;

        public Visibility Visibility
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

                    OnPropertyChanged(nameof(Visibility));
                }
            }
        }
    }
}