using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Media;

namespace FancyZonesEditor.Converters
{
    public class BooleanToBrushConverter : IValueConverter
    {
        private static Brush c_selectedBrush = new SolidColorBrush(Color.FromRgb(0x00, 0x78, 0xD7));
        private static Brush c_normalBrush = new SolidColorBrush(Color.FromRgb(0xF2, 0xF2, 0xF2));

        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return ((bool)value) ? c_selectedBrush : c_normalBrush;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return value == c_selectedBrush;
        }
    }
}
