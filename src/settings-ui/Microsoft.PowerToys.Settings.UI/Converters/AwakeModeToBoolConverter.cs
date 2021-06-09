// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.PowerToys.Settings.UI.Library;
using Windows.UI.Xaml.Data;

namespace Microsoft.PowerToys.Settings.UI.Converters
{
    public sealed class AwakeModeToBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (targetType != typeof(bool?))
            {
                throw new InvalidOperationException("The target type needs to be a boolean.");
            }

            if (parameter == null)
            {
                throw new NullReferenceException("Parameter cannot be null for the PowerToys Awake mode to bool converter.");
            }

            var expectedMode = (AwakeMode)Enum.Parse(typeof(AwakeMode), parameter.ToString());
            var currentMode = (AwakeMode)value;

            return currentMode.Equals(expectedMode);
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            if (parameter == null)
            {
                throw new NullReferenceException("Parameter cannot be null for the PowerToys Awake mode to bool converter.");
            }

            return (AwakeMode)Enum.Parse(typeof(AwakeMode), parameter.ToString());
        }
    }
}
