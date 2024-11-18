// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WorkspacesCsharpLibrary
{
    public class PwaHelper
    {
        private const string ChromeBase = "Google\\Chrome\\User Data\\Default\\Web Applications";
        private const string EdgeBase = "Microsoft\\Edge\\User Data\\Default\\Web Applications";
        private const string ResourcesDir = "Manifest Resources";
        private const string IconsDir = "Icons";
        private const string PwaDirIdentifier = "_CRX_";

        private static List<PwaApp> edgePwaApps = new List<PwaApp>();
        private static List<PwaApp> chromePwaApps = new List<PwaApp>();

        public static int EdgeAppsCount { get => edgePwaApps.Count; }

        public static int ChromeAppsCount { get => chromePwaApps.Count; }

        public PwaHelper()
        {
            edgePwaApps = InitPwaData(EdgeBase);
            chromePwaApps = InitPwaData(ChromeBase);
        }

        private List<PwaApp> InitPwaData(string p_baseDir)
        {
            List<PwaApp> result = new List<PwaApp>();
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

                        result.Add(new PwaApp() { Name = filenameWithoutExtension, IconFilename = iconFile, AppId = appId });
                        break;
                    }
                }

                string resourcesDir = Path.Combine(baseFolderName, ResourcesDir);
                if (Directory.Exists(resourcesDir))
                {
                    foreach (string subDir in Directory.GetDirectories(resourcesDir))
                    {
                        string dirName = Path.GetFileName(subDir);
                        if (result.Any(app => app.AppId == dirName))
                        {
                            continue;
                        }

                        string iconsDir = Path.Combine(subDir, IconsDir);
                        if (Directory.Exists(iconsDir))
                        {
                            foreach (string iconFile in Directory.GetFiles(iconsDir, "*.png"))
                            {
                                string filenameWithoutExtension = Path.GetFileNameWithoutExtension(iconFile);

                                result.Add(new PwaApp() { Name = filenameWithoutExtension, IconFilename = iconFile, AppId = dirName });
                                break;
                            }
                        }
                    }
                }
            }

            return result;
        }

        public static string GetChromeAppIconFile(string pwaAppId)
        {
            var candidates = chromePwaApps.Where(x => x.AppId == pwaAppId).ToList();
            if (candidates.Count > 0)
            {
                return candidates.First().IconFilename;
            }

            return string.Empty;
        }

        public static string GetEdgeAppIconFile(string pwaAppId)
        {
            var candidates = edgePwaApps.Where(x => x.AppId == pwaAppId).ToList();
            if (candidates.Count > 0)
            {
                return candidates.First().IconFilename;
            }

            return string.Empty;
        }
    }
}
