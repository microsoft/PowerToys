using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace ImageResizer.Views
{
    [ValueConversion(typeof(Enum), typeof(string))]
    class BoolValueConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => (bool)value ? Visibility.Visible : Visibility.Collapsed;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
