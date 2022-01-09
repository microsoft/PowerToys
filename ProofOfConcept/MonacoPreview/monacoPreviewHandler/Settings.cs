using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using WK.Libraries.WTL;

namespace MonacoPreviewHandler
{
    /// <summary>
    /// This class defines all the variables used for Monaco
    /// </summary>
    class Settings
    {
        /// <summary>
        /// Theme: dark, light or system.
        /// </summary>
        private readonly string theme = "system";

        /// <summary>
        /// Word warping. Set by PT settings.
        /// </summary>
        public readonly bool wrap = false;

        /// <summary>
        /// Max file size for displaying (in bytes).
        /// </summary>
        public readonly long maxFileSize = 10000;

        /// <summary>
        /// String which is displayed if the file is too big.
        /// </summary>
        public readonly string maxFileSizeErr = Resources.Max_File_Size_Error;

        /// <summary>
        /// The color of the window background.
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
        /// The color of text labels.
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
        public string AssemblyDirectory
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
        public String GetTheme(ThemeListener.ThemeModes systemTheme)
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