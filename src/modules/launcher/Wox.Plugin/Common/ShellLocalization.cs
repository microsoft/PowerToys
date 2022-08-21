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
    /// Reused code from https://stackoverflow.com/questions/41423491/how-to-get-localized-name-of-known-folder for the method <see cref="GetLocalizedName"/>
    /// </summary>
    public class ShellLocalization
    {
        // Cache for already localized names. This makes localization of already localized string faster.
        private Dictionary<string, string> _localizationCache = new Dictionary<string, string>();

        private const uint DONTRESOLVEDLLREFERENCES = 0x00000001;
        private const uint LOADLIBRARYASDATAFILE = 0x00000002;

        [DllImport("shell32.dll", CallingConvention = CallingConvention.Winapi, CharSet = CharSet.Unicode)]
        private static extern int SHGetLocalizedName(string pszPath, StringBuilder pszResModule, ref int cch, out int pidsRes);

        [DllImport("user32.dll", EntryPoint = "LoadStringW", CallingConvention = CallingConvention.Winapi, CharSet = CharSet.Unicode)]
        private static extern int LoadString(IntPtr hModule, int resourceID, StringBuilder resourceValue, int len);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true, EntryPoint = "LoadLibraryExW")]
        private static extern IntPtr LoadLibraryEx(string lpFileName, IntPtr hFile, uint dwFlags);

        [DllImport("kernel32.dll", ExactSpelling = true)]
        private static extern int FreeLibrary(IntPtr hModule);

        [DllImport("kernel32.dll", EntryPoint = "ExpandEnvironmentStringsW", CharSet = CharSet.Unicode, ExactSpelling = true)]
        private static extern uint ExpandEnvironmentStrings(string lpSrc, StringBuilder lpDst, int nSize);

        /// <summary>
        /// Returns the localized name of a shell item.
        /// </summary>
        /// <param name="path">Path to the shell item (e. g. shortcut 'File Explorer.lnk').</param>
        /// <returns>The localized name as string or <see cref="string.Empty"/>.</returns>
        public string GetLocalizedName(string path)
        {
            // Checking cahce if path is already localized
            if (_localizationCache.ContainsKey(path.ToLowerInvariant()))
            {
                return _localizationCache[path.ToLowerInvariant()];
            }

            StringBuilder resourcePath = new StringBuilder(1024);
            StringBuilder localizedName = new StringBuilder(1024);
            int len, id;
            len = resourcePath.Capacity;

            // If there is no resource to localize a file name the method returns a non zero value.
            if (SHGetLocalizedName(path, resourcePath, ref len, out id) == 0)
            {
                _ = ExpandEnvironmentStrings(resourcePath.ToString(), resourcePath, resourcePath.Capacity);
                IntPtr hMod = LoadLibraryEx(resourcePath.ToString(), IntPtr.Zero, DONTRESOLVEDLLREFERENCES | LOADLIBRARYASDATAFILE);
                if (hMod != IntPtr.Zero)
                {
                    if (LoadString(hMod, id, localizedName, localizedName.Capacity) != 0)
                    {
                        string lString = localizedName.ToString();
                        _ = FreeLibrary(hMod);

                        _localizationCache.Add(path.ToLowerInvariant(), lString);
                        return lString;
                    }

                    _ = FreeLibrary(hMod);
                }
            }

            return string.Empty;
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
