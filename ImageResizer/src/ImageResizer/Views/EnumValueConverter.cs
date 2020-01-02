using System;
using System.Globalization;
using System.Text;
using System.Windows.Data;
using ImageResizer.Properties;

namespace ImageResizer.Views
{
    [ValueConversion(typeof(Enum), typeof(string))]
    public class EnumValueConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var type = value.GetType();
            var builder = new StringBuilder();

            builder
                .Append(type.Name)
                .Append("_")
                .Append(Enum.GetName(type, value));

            var toLower = false;
            if ((string)parameter == "ToLower")
            {
                toLower = true;
            }
            else if (parameter != null)
            {
                builder
                    .Append("_")
                    .Append(parameter);
            }

            var targetValue = Resources.ResourceManager.GetString(builder.ToString());

            if (toLower)
                targetValue = targetValue.ToLower();

            return targetValue;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => value;
    }
}
