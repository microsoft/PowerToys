using System;
using System.Windows.Data;
using System.Windows.Markup;

namespace Wox.Plugin.SystemPlugins.Program
{
    public class StringEmptyConverter : MarkupExtension, IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return string.IsNullOrEmpty((string)value) ? parameter : value;
        }

        public object ConvertBack(
              object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotSupportedException();
        }


        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            return this;
        }
    }
}
