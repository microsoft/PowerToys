// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.UI.Xaml.Data;

namespace Microsoft.PowerToys.Settings.UI.Converters
{
    public sealed partial class DateTimeOffsetToStringConverter : IValueConverter
    {
        /// <summary>
        /// Gets or sets default .NET date format string. Can be overridden in XAML via ConverterParameter.
        /// </summary>
        public string Format { get; set; } = "MMMM yyyy";

        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is null)
            {
                return string.Empty;
            }

            var culture = GetCulture(language);
            var format = parameter as string ?? Format;

            if (value is DateTimeOffset dto)
            {
                return dto.ToString(format, culture);
            }

            if (value is DateTime dt)
            {
                return dt.ToString(format, culture);
            }

            if (value is string s)
            {
                // Try to parse strings robustly using the culture; assume unspecified is universal to avoid local offset surprises
                if (DateTimeOffset.TryParse(s, culture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsedDto))
                {
                    return parsedDto.ToString(format, culture);
                }

                if (DateTime.TryParse(s, culture, DateTimeStyles.AssumeLocal, out var parsedDt))
                {
                    return parsedDt.ToString(format, culture);
                }
            }

            return string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }

        private static CultureInfo GetCulture(string language)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(language))
                {
                    return new CultureInfo(language);
                }
            }
            catch
            {
                // ignore and fall back
            }

            // Prefer UI culture for display
            return CultureInfo.CurrentUICulture;
        }
    }
}
