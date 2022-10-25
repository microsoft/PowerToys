// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace PowerToys.FileLocksmithUI.Converters
{
    using System;
    using FileLocksmith.Interop;
    using Microsoft.UI.Xaml.Data;

    public sealed class FileListToDescriptionConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            var paths = (string[])value;
            if (paths.Length == 0)
            {
                return string.Empty;
            }
            else if (paths.Length == 1)
            {
                return paths[0];
            }
            else
            {
                return paths[0] + ", +" + (paths.Length - 1);
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            return value;
        }
    }
}
