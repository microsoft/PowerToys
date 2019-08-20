using System;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace Wox.Plugin.SharedCommands
{
    public static class SearchWeb
    {
        /// <summary> Opens search in a new browser. If no browser path is passed in then Chrome is used. 
        /// Leave browser path blank to use Chrome.
        /// </summary>
        public static void NewBrowserWindow(this string url, string browserPath)
        {
            var browserExecutableName = browserPath?
                                        .Split(new[] { Path.DirectorySeparatorChar }, StringSplitOptions.None)
                                        .Last();

            var browser = string.IsNullOrEmpty(browserExecutableName) ? "chrome" : browserPath;

            // Internet Explorer will open url in new browser window, and does not take the --new-window parameter
            var browserArguements = browserExecutableName == "iexplore.exe" ? url : "--new-window " + url;

            try
            {
                Process.Start(browser, browserArguements);
            }
            catch (System.ComponentModel.Win32Exception)
            {
                Process.Start(url);
            }
        }
    }
}
