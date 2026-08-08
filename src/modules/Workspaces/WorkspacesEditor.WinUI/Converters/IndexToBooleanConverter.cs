// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;

using Microsoft.UI.Xaml.Data;

namespace WorkspacesEditor.Converters
{
    public partial class IndexToBooleanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is int index &&
                parameter is string param &&
                int.TryParse(param, NumberStyles.Integer, CultureInfo.InvariantCulture, out int target))
            {
                return index == target;
            }

            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
