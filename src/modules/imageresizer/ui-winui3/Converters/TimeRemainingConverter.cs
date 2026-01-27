// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.Text;
using ImageResizer.Helpers;
using Microsoft.UI.Xaml.Data;

namespace ImageResizer.Converters
{
    public partial class TimeRemainingConverter : IValueConverter
    {
        private static CompositeFormat _progressTimeRemainingFormat;

        private static CompositeFormat ProgressTimeRemainingFormat
        {
            get
            {
                if (_progressTimeRemainingFormat == null)
                {
                    var formatString = ResourceLoaderInstance.ResourceLoader.GetString("Progress_TimeRemaining");
                    _progressTimeRemainingFormat = CompositeFormat.Parse(formatString);
                }

                return _progressTimeRemainingFormat;
            }
        }

        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is TimeSpan timeSpan)
            {
                if (timeSpan == TimeSpan.MaxValue || timeSpan.TotalSeconds < 1)
                {
                    return string.Empty;
                }

                var culture = string.IsNullOrEmpty(language) ? CultureInfo.CurrentCulture : new CultureInfo(language);
                return string.Format(culture, ProgressTimeRemainingFormat, timeSpan);
            }

            return string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            return value;
        }
    }
}
