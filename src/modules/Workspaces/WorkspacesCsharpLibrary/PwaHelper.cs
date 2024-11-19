// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

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
            var candidates = pwaApps.Where(x => x.AppId == pwaAppId).ToList();
            if (candidates.Count > 0)
            {
                return candidates.First().IconFilename;
            }

            return string.Empty;
        }
    }
}
