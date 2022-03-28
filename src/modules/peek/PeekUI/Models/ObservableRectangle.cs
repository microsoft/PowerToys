namespace PeekUI.Models
{
    public class ObservableRectangle : ObservableObject
    {
        private double _left;

        public double Left
        {
            get
            {
                return _left;
            }

            set
            {
                if (_left != value)
                {
                    _left = value;
                    OnPropertyChanged(nameof(Left));
                }
            }
        }

        private double _top;

        public double Top
        {
            get
            {
                return _top;
            }

            set
            {
                if (_top != value)
                {
                    _top = value;
                    OnPropertyChanged(nameof(Top));
                }
            }
        }

        private double _height;

        public double Height
        {
            get
            {
                return _height;
            }

            set
            {
                if (_height != value)
                {
                    _height = value;
                    OnPropertyChanged(nameof(Height));
                }
            }
        }

        private double _width;

        public double Width
        {
            get
            {
                return _width;
            }

            set
            {
                if (_width != value)
                {
                    _width = value;
                    OnPropertyChanged(nameof(Width));
                }
            }
        }
    }
}