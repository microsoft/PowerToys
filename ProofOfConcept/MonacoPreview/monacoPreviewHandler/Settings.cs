using System;
using WK.Libraries.WTL;

namespace MonacoPreviewHandler
{
    class Settings
    {
        // This class contains all the settings, later set by other sources

        // Theme "dark" or "light" or "system". Set by PT settings
        private readonly string theme = "system";

        // Word warping. Set by PT settings
        public readonly bool wrap = false;

        // URL to the page
        public readonly string BaseUrl = "file://" + AppContext.BaseDirectory + "/index.html";

        // Max file size for displaying (in bytes)
        public readonly long maxFileSize = 10000; 
        
        // String which is displayed if the file is too big
        public readonly string maxFileSizeErr = "This file is too big to display.\nMax file size: 10KB";
        
        // Returns the theme that should be used
        public String GetTheme(ThemeListener.ThemeModes t)
        {

            if (this.theme == "system")
            {
                if (t == ThemeListener.ThemeModes.Light)
                {
                    return "light";
                }
                else if (t == ThemeListener.ThemeModes.Dark)
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