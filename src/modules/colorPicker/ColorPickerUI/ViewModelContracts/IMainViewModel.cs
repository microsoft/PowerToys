using System.Windows.Media;

namespace ColorPicker.ViewModelContracts
{
    public interface IMainViewModel
    {
        string HexColor { get; }

        string RgbColor { get; }

        Brush ColorBrush { get; }
    }
}
