// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.PowerToys.Settings.UI.Library;
using Windows.UI.Xaml.Data;

namespace Microsoft.PowerToys.Settings.UI.Converters
{
    public sealed class EspressoModeToReverseBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (targetType != typeof(bool))
            {
                throw new InvalidOperationException("The target type needs to be a boolean.");
            }

            switch ((EspressoMode)value)
            {
                case EspressoMode.INDEFINITE:
                    return false;
                case EspressoMode.TIMED:
                    return true;
            }

            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            switch ((bool)value)
            {
                case true:
                    return EspressoMode.INDEFINITE;
                case false:
                    return EspressoMode.TIMED;
            }

            return EspressoMode.INDEFINITE;
        }
    }
}
