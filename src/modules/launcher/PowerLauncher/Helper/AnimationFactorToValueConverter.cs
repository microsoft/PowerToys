// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// https://github.com/dotnet/wpf/blob/main/src/Microsoft.DotNet.Wpf/src/Themes/PresentationFramework.Fluent/Controls/AnimationFactorToValueConverter.cs
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Fluent.Controls
#pragma warning restore IDE0130 // Namespace does not match folder structure
{
    internal sealed class AnimationFactorToValueConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values[0] is not double completeValue)
            {
                return 0.0;
            }

            if (values[1] is not double factor)
            {
                return 0.0;
            }

            if (parameter is "negative")
            {
                factor = -factor;
            }

            return factor * completeValue;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
