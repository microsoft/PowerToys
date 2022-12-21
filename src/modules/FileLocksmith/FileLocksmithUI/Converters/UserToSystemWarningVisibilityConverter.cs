// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using FileLocksmith.Interop;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace PowerToys.FileLocksmithUI.Converters
{
    public sealed class UserToSystemWarningVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            string user = ((string)value).ToUpperInvariant().Trim();
            if (user.Equals("SYSTEM", StringComparison.Ordinal) || user.Equals("LOCALSYSTEM", StringComparison.Ordinal))
            {
                return Visibility.Visible;
            }
            else
            {
                return Visibility.Collapsed;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotSupportedException();
        }
    }
}
