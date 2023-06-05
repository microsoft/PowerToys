// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.PowerToys.PreviewHandler.Markdown
{
    internal sealed class Settings
    {
        /// <summary>
        /// Gets the color of the window background.
        /// </summary>
        public static Color BackgroundColor
        {
            get
            {
                if (GetTheme() == "dark")
                {
                    return System.Drawing.ColorTranslator.FromHtml("#1e1e1e");
                }
                else
                {
                    return Color.White;
                }
            }
        }

        /// <summary>
        /// Returns the theme.
        /// </summary>
        /// <returns>Theme that should be used.</returns>
        public static string GetTheme()
        {
            return Common.UI.ThemeManager.GetWindowsBaseColor().ToLowerInvariant();
        }
    }
}
