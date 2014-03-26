using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Data;
using System.Windows.Markup;
using Wox.Infrastructure.Storage.UserSettings;

namespace Wox
{
    public class OpacityModeConverter : MarkupExtension, IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (!(value is OpacityMode)) return value.ToString();

            var mode = (OpacityMode) value;
            switch (mode)
            {
                case OpacityMode.Normal:
                    return "Normal Window";
                case OpacityMode.LayeredWindow:
                {
                    if (Environment.OSVersion.Version.Major < 5)
                        return "Layered Window (not supported by your Windows)";
                    if (Environment.OSVersion.Version.Major == 5)
                        return "Layered Window (not recommended on your Windows)";
                    return "Layered Window";
                }
                case OpacityMode.DWM:
                {
                    if (Environment.OSVersion.Version.Major < 6)
                        return "DWM-Enabled Window (not supported by your Windows)";
                    return "DWM-Enabled Window";
                }
            }
            return value.ToString();
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
