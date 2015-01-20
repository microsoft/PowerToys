using System;
using System.Globalization;
using Wox.Core.UserSettings;

namespace Wox.Converters
{
    public class OpacityModeConverter : ConvertorBase<OpacityModeConverter>
    {
        public override object Convert(object value, Type targetType, object parameter, CultureInfo culture)
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

        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            return this;
        }
    }
}