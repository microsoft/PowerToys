// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Reflection;
using System.Windows.Media.Imaging;

namespace FileActionsMenu.Ui.Helpers
{
    public sealed class IconHelper
    {
        public static BitmapImage GetIconFromModuleName(string moduleName)
        {
            var outputDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? throw new InvalidOperationException();
            return new BitmapImage() { UriSource = new Uri(Path.Combine(outputDirectory, "WinUI3Apps\\Assets\\Settings\\FluentIcons\\FluentIcons" + moduleName + ".png")) };
        }
    }
}
