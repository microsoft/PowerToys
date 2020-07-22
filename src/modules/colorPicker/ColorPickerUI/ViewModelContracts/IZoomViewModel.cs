using System.Windows.Media.Imaging;

namespace ColorPicker.ViewModelContracts
{
    public interface IZoomViewModel
    {
        BitmapSource ZoomArea { get; set; }

        double ZoomFactor { get; set; }

        double DesiredWidth { get; set; }

        double DesiredHeight { get; set; }

        double Width { get; set; }

        double Height { get; set; }
    }
}
