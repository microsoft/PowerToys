using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media.Imaging;
using ImageResizer.Properties;

namespace ImageResizer.Views
{
    [ValueConversion(typeof(TiffCompressOption), typeof(string))]
    class TiffCompressOptionConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => Resources.ResourceManager.GetString(
                "TiffCompressOption_" + Enum.GetName(typeof(TiffCompressOption), value));

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
