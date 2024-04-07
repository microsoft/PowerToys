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
        /// <summary>
        /// Gets a <see cref="BitmapIcon"/> from a module name.
        /// </summary>
        /// <param name="moduleName">Name of the module.</param>
        /// <returns>The <see cref="BitmapIcon"/> containing the icon of the module.</returns>
        public static BitmapIcon GetIconFromModuleName(string moduleName)
        {
            var outputDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? throw new InvalidOperationException();
            return new BitmapIcon() { UriSource = new Uri(Path.Combine(outputDirectory, "Assets\\Settings\\Icons\\" + moduleName + ".png")), ShowAsMonochrome = false };
        }
    }
}
