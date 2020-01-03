// Copyright (c) Brice Lambson
// The Brice Lambson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.  Code forked from Brice Lambson's https://github.com/bricelam/ImageResizer/

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
