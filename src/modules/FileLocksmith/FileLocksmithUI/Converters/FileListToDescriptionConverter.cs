// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using FileLocksmith.Interop;
using Microsoft.UI.Xaml.Data;

namespace PowerToys.FileLocksmithUI.Converters
{
    public sealed class FileListToDescriptionConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            var paths = (string[])value;
            if (paths.Length == 0)
            {
                return string.Empty;
            }

            string firstPath = paths[0];
            firstPath = Path.GetFileName(paths[0]);
            if (string.IsNullOrEmpty(firstPath))
            {
                firstPath = Path.GetDirectoryName(paths[0]);
            }

            if (string.IsNullOrEmpty(firstPath))
            {
                firstPath = Path.GetPathRoot(paths[0]);
            }

            if (paths.Length == 1)
            {
                return firstPath;
            }
            else
            {
                return firstPath + "; +" + (paths.Length - 1);
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            return value;
        }
    }
}
