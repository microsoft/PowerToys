// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using Microsoft.PowerToys.PreviewHandler.Monaco.Properties;
using WK.Libraries.WTL;

namespace Microsoft.PowerToys.PreviewHandler.Monaco
{
    /// <summary>
    /// This class defines all the variables used for Monaco
    /// </summary>
    public class Settings
    {
        /// <summary>
        /// Theme: dark, light or system.
        /// </summary>
        private readonly string theme = "system";

        /// <summary>
        /// Word warping. Set by PT settings.
        /// </summary>
        private bool _wrap;

        public bool Wrap
        {
            get => _wrap;
            set
            {
                _wrap = value;
            }
        }

        /// <summary>
        /// Max file size for displaying (in bytes).
        /// </summary>
        private readonly long _maxFileSize = 50000;

        public long MaxFileSize => _maxFileSize;

        /// <summary>
        /// Gets the color of the window background.
        /// </summary>
        public Color BackgroundColor
        {
            get
            {
                if (this.GetTheme(ThemeListener.AppMode) == "dark")
                {
                    return Color.DimGray;
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
        public Color TextColor
        {
            get
            {
                if (this.GetTheme(ThemeListener.AppMode) == "dark")
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
                string codeBase = Assembly.GetExecutingAssembly().CodeBase;
                UriBuilder uri = new UriBuilder(codeBase);
                string path = Uri.UnescapeDataString(uri.Path);
                return Path.GetDirectoryName(path);
            }
        }

        /// <summary>
        /// Returns the the theme.
        /// </summary>
        /// <param name="systemTheme">theme to use when it's set theme is set to system theme.</param>
        /// <returns>Theme that should be used.</returns>
        public string GetTheme(ThemeListener.ThemeModes systemTheme)
        {
            if (this.theme == "system")
            {
                if (systemTheme == ThemeListener.ThemeModes.Light)
                {
                    return "light";
                }
                else if (systemTheme == ThemeListener.ThemeModes.Dark)
                {
                    return "dark";
                }
                else
                {
                    Console.WriteLine("Unknown theme.");
                    return "light";
                }
            }
            else
            {
                return this.theme;
            }
        }
    }
}
