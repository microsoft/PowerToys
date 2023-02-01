// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using FileLocksmith.Interop;
using Microsoft.UI.Xaml.Data;

namespace PowerToys.FileLocksmithUI.Converters
{
    public sealed class FileCountConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
#pragma warning disable CA1305 // Specify IFormatProvider
            return ((string[])value).Length.ToString();
#pragma warning restore CA1305 // Specify IFormatProvider
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            return value;
        }
    }
}
