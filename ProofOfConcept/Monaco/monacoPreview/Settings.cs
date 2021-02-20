using System;
using System.Collections.Generic;
using System.Text;
using Windows.UI.ViewManagement;
using WK.Libraries.WTL;

namespace monacoPreview
{
    class Settings
    {
        // This class contains all the settings, later set by other sources

        // Theme "dark" or "light" or "system". Set by PT settings
        private readonly string theme = "system";

        // Word warping. Set by PT settings
        public bool wrap = true;

        // URL to the page
        public string baseURL = "file://" + System.AppContext.BaseDirectory + "/index.html";
        

        public String GetTheme(ThemeListener.ThemeModes t)
        {
            // Puts out the theme that should be used
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
