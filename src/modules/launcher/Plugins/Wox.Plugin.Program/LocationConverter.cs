using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Markup;

namespace Wox.Plugin.Program
{
    public class LocationConverter : MarkupExtension, IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var text = value as string;
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }
            else
            {
                return text;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }

        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            return this;
        }
    }
}
