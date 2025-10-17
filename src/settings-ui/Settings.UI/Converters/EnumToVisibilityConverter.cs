// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace Microsoft.PowerToys.Settings.UI.Converters
{
    public partial class EnumToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value == null)
            {
                return Visibility.Collapsed;
            }

            var mode = value.ToString();

            if (mode.Equals("Off", StringComparison.OrdinalIgnoreCase))
            {
                return Visibility.Collapsed;
            }

            // Allow multiple targets separated by commas (e.g. "FixedHours,SunsetToSunrise")
            var targetString = parameter?.ToString() ?? string.Empty;
            var targets = targetString
                .Split(',')
                .Select(t => t.Trim())
                .ToList();

            // Otherwise, show only if the current mode is in the list of targets
            return targets.Contains(mode)
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
            => throw new NotImplementedException();
    }
}
