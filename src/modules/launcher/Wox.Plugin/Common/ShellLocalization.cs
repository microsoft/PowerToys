// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
using System;
using System.Runtime.InteropServices;
using System.Text;

namespace Wox.Plugin.Common
{
    /// <summary>
    /// Class to get localized name of shell items like 'My computer'. The localization is based on the 'windows display language'.
    /// Reused code from https://stackoverflow.com/questions/41423491/how-to-get-localized-name-of-known-folder
    /// </summary>
    public static class ShellLocalization
    {
        [DllImport("shell32.dll", CallingConvention = CallingConvention.Winapi, CharSet = CharSet.Unicode)]
        internal static extern int SHGetLocalizedName(string pszPath, StringBuilder pszResModule, ref int cch, out int pidsRes);

        [DllImport("user32.dll", EntryPoint = "LoadStringW", CallingConvention = CallingConvention.Winapi, CharSet = CharSet.Unicode)]
        internal static extern int LoadString(IntPtr hModule, int resourceID, StringBuilder resourceValue, int len);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true, EntryPoint = "LoadLibraryExW")]
        internal static extern IntPtr LoadLibraryEx(string lpFileName, IntPtr hFile, uint dwFlags);

        internal const uint DONTRESOLVEDLLREFERENCES = 0x00000001;
        internal const uint LOADLIBRARYASDATAFILE = 0x00000002;

        [DllImport("kernel32.dll", ExactSpelling = true)]
        internal static extern int FreeLibrary(IntPtr hModule);

        [DllImport("kernel32.dll", EntryPoint = "ExpandEnvironmentStringsW", CharSet = CharSet.Unicode, ExactSpelling = true)]
        internal static extern uint ExpandEnvironmentStrings(string lpSrc, StringBuilder lpDst, int nSize);

        /// <summary>
        /// Returns the localized name of a shell item.
        /// </summary>
        /// <param name="path">Path to the shell item (e. g. shortcut 'File Explorer.lnk').</param>
        /// <returns>The localized name as string or <see cref="string.Empty"/>.</returns>
        public static string GetLocalizedName(string path)
        {
            StringBuilder resourcePath = new StringBuilder(1024);
            StringBuilder localizedName = new StringBuilder(1024);
            int len, id;
            len = resourcePath.Capacity;

            if (SHGetLocalizedName(path, resourcePath, ref len, out id) == 0)
            {
                _ = ExpandEnvironmentStrings(resourcePath.ToString(), resourcePath, resourcePath.Capacity);
                IntPtr hMod = LoadLibraryEx(resourcePath.ToString(), IntPtr.Zero, DONTRESOLVEDLLREFERENCES | LOADLIBRARYASDATAFILE);
                if (hMod != IntPtr.Zero)
                {
                    if (LoadString(hMod, id, localizedName, localizedName.Capacity) != 0)
                    {
                        string lSTring = localizedName.ToString();
                        _ = FreeLibrary(hMod);
                        return lSTring;
                    }

                    _ = FreeLibrary(hMod);
                }
            }

            return string.Empty;
        }
    }
}
