using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using Wox.Plugin;

namespace Wox.Converters
{
    public class StringNullOrEmptyToVisibilityConverter : ConvertorBase<StringNullOrEmptyToVisibilityConverter>
    {
        public StringNullOrEmptyToVisibilityConverter() {  }

        public override object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return string.IsNullOrEmpty(value as string) ? Visibility.Collapsed : Visibility.Visible;
        }

        public override object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return null;
        }

        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            return this;
        }
    }

    public class ContextMenuEmptyToWidthConverter : ConvertorBase<ContextMenuEmptyToWidthConverter>
    {
        public override object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            List<Result> results = value as List<Result>;
            return results == null || results.Count == 0 ? 0 : 17;
        }

        public override object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return null;
        }

        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            return this;
        }
    }
}