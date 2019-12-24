using System;
using System.Globalization;
using System.Windows.Data;
using ImageResizer.Properties;

namespace ImageResizer.Views
{
    [ValueConversion(typeof(double), typeof(string))]
    class AutoDoubleConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var d = (double)value;

            return d != 0
                ? d.ToString(culture)
                : (string)parameter == "Auto"
                    ? Resources.Input_Auto
                    : string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var text = (string)value;

            return !string.IsNullOrEmpty(text)
                ? double.Parse(text, culture)
                : 0;
        }
    }
}
