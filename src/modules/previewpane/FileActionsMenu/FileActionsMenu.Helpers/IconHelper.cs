// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Reflection;
using Microsoft.UI.Xaml.Controls;

namespace FileActionsMenu.Ui.Helpers
{
    public sealed class IconHelper
    {
        public static BitmapIcon GetIconFromModuleName(string moduleName)
        {
            var outputDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? throw new InvalidOperationException();
            return new BitmapIcon() { UriSource = new Uri(Path.Combine(outputDirectory, "Assets\\Settings\\FluentIcons\\FluentIcons" + moduleName + ".png")), ShowAsMonochrome = false };
        }
    }
}
