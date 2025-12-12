// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.UI.Xaml.Data;

namespace Microsoft.PowerToys.Settings.UI.Controls.Converters
{
    public partial class ModuleListSortOptionToBooleanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is ModuleListSortOption sortOption && parameter is string paramString)
            {
                if (Enum.TryParse(typeof(ModuleListSortOption), paramString, out object? result) && result != null)
                {
                    return sortOption == (ModuleListSortOption)result;
                }
            }

            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            if (value is bool isChecked && isChecked && parameter is string paramString)
            {
                if (Enum.TryParse(typeof(ModuleListSortOption), paramString, out object? result) && result != null)
                {
                    return (ModuleListSortOption)result;
                }
            }

            return ModuleListSortOption.Alphabetical;
        }
    }
}
