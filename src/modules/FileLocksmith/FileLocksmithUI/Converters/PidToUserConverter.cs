// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace PowerToys.FileLocksmithUI.Converters
{
    using System;
    using FileLocksmith.Interop;
    using Microsoft.UI.Xaml.Data;

    public sealed class PidToUserConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            return NativeMethods.PidToUser((uint)value);
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            return value;
        }
    }
}
