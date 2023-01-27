// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace Wox.Plugin.Common
{
    /// <summary>
    /// Class to get localized name of shell items like 'My computer'. The localization is based on the 'windows display language'.
    /// </summary>
    public class ShellLocalization
    {
        // Guid for IShellItem object type
        private const string IShellItemGuid = "43826d1e-e718-42ee-bc55-a1e261c37bfe";

        // Cache for already localized names. This makes localization of already localized string faster.
        private Dictionary<string, string> _localizationCache = new Dictionary<string, string>();

        [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern int SHCreateItemFromParsingName(
        [MarshalAs(UnmanagedType.LPWStr)] string path,
        IntPtr pbc,
        ref Guid riid,
        [MarshalAs(UnmanagedType.Interface)] out IShellItem shellItem);

        /// <summary>
        /// Returns the localized name of a shell item.
        /// </summary>
        /// <param name="path">Path to the shell item (e. g. shortcut 'File Explorer.lnk').</param>
        /// <returns>The localized name as string or <see cref="string.Empty"/>.</returns>
        public string GetLocalizedName(string path)
        {
            // If it is a drive letter return it
            if (path.Length == 2 & path.EndsWith(':'))
            {
                return path;
            }

            // Checking cache if path is already localized
            if (_localizationCache.ContainsKey(path.ToLowerInvariant()))
            {
                return _localizationCache[path.ToLowerInvariant()];
            }

            Guid shellItemType = new Guid(IShellItemGuid);
            int retCode = SHCreateItemFromParsingName(path, IntPtr.Zero, ref shellItemType, out IShellItem shellItem);

            if (retCode != 0)
            {
                return string.Empty;
            }

            string filename;
            shellItem.GetDisplayName(SIGDN.NORMALDISPLAY, out filename);

            if (!_localizationCache.ContainsKey(path.ToLowerInvariant()))
            {
                // The if condition is required to not get timing problems when called from an parallel execution.
                // Without the check we got "key exists" crashes.
                _localizationCache.Add(path.ToLowerInvariant(), filename);
            }

            return filename;
        }

        /// <summary>
        /// This method returns the localized path to a shell item (folder or file)
        /// </summary>
        /// <param name="path">The path to localize</param>
        /// <returns>The localized path or the original path if localized version is not available</returns>
        public string GetLocalizedPath(string path)
        {
            path = Environment.ExpandEnvironmentVariables(path);
            string ext = Path.GetExtension(path);
            var pathParts = path.Split("\\");
            string[] locPath = new string[pathParts.Length];

            for (int i = 0; i < pathParts.Length; i++)
            {
                int iElements = i + 1;
                string lName = GetLocalizedName(string.Join("\\", pathParts[..iElements]));
                locPath[i] = !string.IsNullOrEmpty(lName) ? lName : pathParts[i];
            }

            string newPath = string.Join("\\", locPath);
            newPath = !newPath.EndsWith(ext, StringComparison.InvariantCultureIgnoreCase) ? newPath + ext : newPath;

            return newPath;
        }
    }
}
