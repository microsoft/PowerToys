using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Media;

namespace Microsoft.PowerToys.Settings.UI.Converters
{
    public sealed class ModuleEnabledToForegroundConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            bool isEnabled = (bool)value;
            if (isEnabled)
            {
                return new SolidColorBrush((Color)Application.Current.Resources["SystemBaseHighColor"]);
            }
            else
            {
                return (SolidColorBrush)Application.Current.Resources["SystemControlDisabledBaseMediumLowBrush"];
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            return value;
        }
    }
}