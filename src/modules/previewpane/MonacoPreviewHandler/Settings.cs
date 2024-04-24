// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using Microsoft.PowerToys.Settings.UI.Library;

namespace Microsoft.PowerToys.PreviewHandler.Monaco
{
    /// <summary>
    /// This class defines all the variables used for Monaco
    /// </summary>
    public class Settings
    {
        private static SettingsUtils moduleSettings = new SettingsUtils();

        /// <summary>
        /// Gets a value indicating whether word wrapping should be applied. Set by PT settings.
        /// </summary>
        public bool Wrap
        {
            get
            {
                try
                {
                    return moduleSettings.GetSettings<PowerPreviewSettings>(PowerPreviewSettings.ModuleName).Properties.EnableMonacoPreviewWordWrap;
                }
                catch (FileNotFoundException)
                {
                    // Couldn't read the settings.
                    // Assume default of true.
                    return true;
                }
            }
        }

        /// <summary>
        /// Gets a value indicating whether to try formatting the file. Set by PT settings.
        /// </summary>
        public bool TryFormat
        {
            get
            {
                try
                {
                    return moduleSettings.GetSettings<PowerPreviewSettings>(PowerPreviewSettings.ModuleName).Properties.MonacoPreviewTryFormat;
                }
                catch (FileNotFoundException)
                {
                    // Couldn't read the settings.
                    // Assume default of false.
                    return false;
                }
            }
        }

        /// <summary>
        /// Gets Max file size for displaying (in bytes).
        /// </summary>
        public double MaxFileSize
        {
            get
            {
                try
                {
                    return moduleSettings.GetSettings<PowerPreviewSettings>(PowerPreviewSettings.ModuleName).Properties.MonacoPreviewMaxFileSize.Value * 1000;
                }
                catch (FileNotFoundException)
                {
                    // Couldn't read the settings.
                    // Assume default of 50000.
                    return 50000;
                }
            }
        }

        /// <summary>
        /// Gets the font size for the previewer. Set by PT settings.
        /// </summary>
        public double FontSize
        {
            get
            {
                try
                {
                    return moduleSettings.GetSettings<PowerPreviewSettings>(PowerPreviewSettings.ModuleName).Properties.MonacoPreviewFontSize.Value;
                }
                catch (FileNotFoundException)
                {
                    // Couldn't read the settings.
                    // Assume default of 14.
                    return 14;
                }
            }
        }

        /// <summary>
        /// Gets a value indicating whether sticky scroll should be enabled. Set by PT settings.
        /// </summary>
        public bool StickyScroll
        {
            get
            {
                try
                {
                    return moduleSettings.GetSettings<PowerPreviewSettings>(PowerPreviewSettings.ModuleName).Properties.MonacoPreviewStickyScroll;
                }
                catch (FileNotFoundException)
                {
                    // Couldn't read the settings.
                    // Assume default of true.
                    return true;
                }
            }
        }

        /// <summary>
        /// Gets the color of the window background.
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
        /// Gets the color of text labels.
        /// </summary>
        public static Color TextColor
        {
            get
            {
                if (GetTheme() == "dark")
                {
                    return Color.White;
                }
                else
                {
                    return Color.Black;
                }
            }
        }

        /// <summary>
        /// Gets the path of the current assembly.
        /// </summary>
        /// <remarks>
        /// Source: https://stackoverflow.com/a/283917/14774889
        /// </remarks>
        public static string AssemblyDirectory
        {
            get
            {
                string codeBase = Assembly.GetExecutingAssembly().Location;
                UriBuilder uri = new UriBuilder(codeBase);
                string path = Uri.UnescapeDataString(uri.Path);
                return Path.GetDirectoryName(path);
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
