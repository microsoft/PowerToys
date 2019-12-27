using System;
using System.Globalization;
using System.Windows.Data;
using ImageResizer.ViewModels;

namespace ImageResizer.Views
{
    [ValueConversion(typeof(Guid), typeof(string))]
    public class ContainerFormatConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => AdvancedViewModel.EncoderMap.TryGetValue((Guid)value, out var result)
                ? result
                : value.ToString();

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
