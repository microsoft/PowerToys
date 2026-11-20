// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace WorkspacesCsharpLibrary
{
    public class PwaHelper
    {
        private const string ChromeBase = "Google\\Chrome\\User Data\\Default\\Web Applications";
        private const string EdgeBase = "Microsoft\\Edge\\User Data\\Default\\Web Applications";
        private const string ResourcesDir = "Manifest Resources";
        private const string IconsDir = "Icons";
        private const string PwaDirIdentifier = "_CRX_";

        private static List<PwaApp> pwaApps = new List<PwaApp>();

        public PwaHelper()
        {
            InitPwaData(EdgeBase);
            InitPwaData(ChromeBase);
        }

        private void InitPwaData(string p_baseDir)
        {
            var baseFolderName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), p_baseDir);
            if (Directory.Exists(baseFolderName))
            {
                foreach (string subDir in Directory.GetDirectories(baseFolderName))
                {
                    string dirName = Path.GetFileName(subDir);
                    if (!dirName.StartsWith(PwaDirIdentifier, StringComparison.InvariantCultureIgnoreCase))
                    {
                        continue;
                    }

                    string appId = dirName.Substring(PwaDirIdentifier.Length, dirName.Length - PwaDirIdentifier.Length).Trim('_');

                    foreach (string iconFile in Directory.GetFiles(subDir, "*.ico"))
                    {
                        string filenameWithoutExtension = Path.GetFileNameWithoutExtension(iconFile);

                        pwaApps.Add(new PwaApp() { Name = filenameWithoutExtension, IconFilename = iconFile, AppId = appId });
                        break;
                    }
                }

                string resourcesDir = Path.Combine(baseFolderName, ResourcesDir);
                if (Directory.Exists(resourcesDir))
                {
                    foreach (string subDir in Directory.GetDirectories(resourcesDir))
                    {
                        string dirName = Path.GetFileName(subDir);
                        if (pwaApps.Any(app => app.AppId == dirName))
                        {
                            continue;
                        }

                        string iconsDir = Path.Combine(subDir, IconsDir);
                        if (Directory.Exists(iconsDir))
                        {
                            foreach (string iconFile in Directory.GetFiles(iconsDir, "*.png"))
                            {
                                string filenameWithoutExtension = Path.GetFileNameWithoutExtension(iconFile);

                                pwaApps.Add(new PwaApp() { Name = filenameWithoutExtension, IconFilename = iconFile, AppId = dirName });
                                break;
                            }
                        }
                    }
                }
            }
        }

        public static string GetPwaIconFilename(string pwaAppId)
        {
            if (string.IsNullOrEmpty(pwaAppId))
            {
                return string.Empty;
            }

            return pwaApps.FirstOrDefault(app => app.AppId == pwaAppId)?.IconFilename ?? string.Empty;
        }

        internal static bool HasLaunchTarget(string browserPath, string appUserModelId, string version)
        {
            // AppLauncher substitutes a sibling proxy for these two browser executables.
            string executable = Path.GetFileName(browserPath);
            string fallbackPath = executable is "msedge.exe" or "chrome.exe" ? Path.ChangeExtension(browserPath, null) + "_proxy.exe" : browserPath;
            if (File.Exists(fallbackPath))
            {
                return true;
            }

            if (string.IsNullOrEmpty(appUserModelId) ||
                !int.TryParse(version, NumberStyles.Integer, CultureInfo.InvariantCulture, out int snapshotVersion) ||
                snapshotVersion < 1)
            {
                return false;
            }

            int result = SHParseDisplayName(@"shell:AppsFolder\" + appUserModelId, IntPtr.Zero, out IntPtr itemIdList, 0, IntPtr.Zero);
            bool registered = result >= 0 && itemIdList != IntPtr.Zero;
            Marshal.FreeCoTaskMem(itemIdList);
            return registered;
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
        private static extern int SHParseDisplayName(string name, IntPtr bindContext, out IntPtr itemIdList, uint attributesIn, IntPtr attributesOut);
    }
}
