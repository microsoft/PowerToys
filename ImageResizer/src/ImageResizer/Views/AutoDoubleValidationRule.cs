using System.Globalization;
using System.Windows.Controls;

namespace ImageResizer.Views
{
    class AutoDoubleValidationRule : ValidationRule
    {
        public override ValidationResult Validate(object value, CultureInfo cultureInfo)
        {
            var text = (string)value;

            return new ValidationResult(
                string.IsNullOrEmpty(text)
                    || double.TryParse(text, NumberStyles.AllowThousands | NumberStyles.Float, cultureInfo, out var _),
                null);
        }
    }
}
