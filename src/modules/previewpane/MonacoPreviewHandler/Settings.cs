// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Drawing;
using System.IO;
using System.Reflection;

namespace Microsoft.PowerToys.PreviewHandler.Monaco
{
    /// <summary>
    /// This class defines all the variables used for Monaco
    /// </summary>
    public class Settings
    {
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
        public static Color BackgroundColor
        {
            get
            {
                if (GetTheme() == "dark")
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
