using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Markup;

namespace Bootstrapper.ViewModels.Util
{
  /// <summary>
  ///   Converts a <see cref="bool" /> to a <see cref="Visibility" />. If the <see cref="Negate" /> property
  ///   is <see langword="false" />, then <see langword="true" /> converts to <see cref="Visibility.Visible" />
  ///   while <see langword="false" /> converts to <see cref="Visibility.Collapsed" />. If <see cref="Negate" />
  ///   is <see langword="true" />, then the negated bound value is used for the conversion.
  /// </summary>
  [ValueConversion(typeof(bool), typeof(Visibility))]
  public class BooleanVisibilityConverter : MarkupExtension, IValueConverter
  {
    /// <summary>
    ///   If <see langword="true" />, will use the negated value of the bound property to perform the conversion. So,
    ///   <see langword="true" /> will convert to <see cref="Visibility.Collapsed" /> while <see langword="false" />
    ///   will convert to <see cref="Visibility.Visible" />.
    /// </summary>
    public bool Negate { get; set; }

    /// <inheritdoc />
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
      if (targetType != typeof(Visibility) && targetType != typeof(Visibility?))
        return Visibility.Collapsed;

      var b = value as bool?;
      if (b == null)
        return Visibility.Collapsed;

      if (Negate)
      {
        if (b.Value)
          return Visibility.Collapsed;

        return Visibility.Visible;
      }

      if (b.Value)
        return Visibility.Visible;

      return Visibility.Collapsed;
    }

    /// <inheritdoc />
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
      if ((targetType == typeof(bool) || targetType == typeof(bool?)) && value is Visibility vis)
      {
        if (Negate)
          return vis != Visibility.Visible;

        return vis == Visibility.Visible;
      }

      return false;
    }

    /// <inheritdoc />
    public override object ProvideValue(IServiceProvider serviceProvider)
    {
      return this;
    }
  }
}