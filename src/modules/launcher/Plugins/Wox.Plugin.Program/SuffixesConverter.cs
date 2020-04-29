using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Markup;

namespace Wox.Plugin.Program
{
    public class SuffixesConvert : MarkupExtension, IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var text = value as string[];
            if (text != null)
            {
                return string.Join(";", text);
            }
            else
            {
                return string.Empty;
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
