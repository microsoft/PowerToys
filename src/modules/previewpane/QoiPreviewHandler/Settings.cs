// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.PowerToys.PreviewHandler.Qoi
{
    internal sealed class Settings
    {
        /// <summary>
        /// Gets the color of the window background.
        /// Even though this is not a setting yet, it's retrieved from a "Settings" class to be aligned with other preview handlers that contain this setting.
        /// It's possible it can be converted into a setting in the future.
        /// </summary>
        public static Color BackgroundColor
        {
            get
            {
                if (GetTheme() == "dark")
                {
                    return Color.FromArgb(30, 30, 30); // #1e1e1e
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
