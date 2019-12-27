using System;
using System.Globalization;
using System.Windows.Data;
using ImageResizer.Models;
using ImageResizer.Properties;

namespace ImageResizer.Views
{
    [ValueConversion(typeof(ResizeUnit), typeof(string))]
    class ResizeUnitConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var output = Resources.ResourceManager.GetString(Enum.GetName(typeof(ResizeUnit), value));

            if ((string)parameter == "ToLower")
            {
                output = output.ToLower(culture);
            }

            return output;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
