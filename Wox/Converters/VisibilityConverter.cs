using System;
using System.Globalization;
using System.Windows;

namespace Wox.Converters
{
    class VisibilityConverter : ConvertorBase<VisibilityConverter>
    {
        public override object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || value == DependencyProperty.UnsetValue)
            {
                return null;
            }

            return bool.Parse(value.ToString()) ? Visibility.Visible : Visibility.Collapsed;
        }

        public override object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return null;
        }
    }

}
