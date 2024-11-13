// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace WorkspacesLauncherUI.Utils
{
    public class PwaHelper
    {
        private const string ChromeBase = "Google\\Chrome\\User Data\\Default\\Web Applications";
        private const string EdgeBase = "Microsoft\\Edge\\User Data\\Default\\Web Applications";
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
            }

            return result;
        }

        internal static List<string> GetEdgeAppsList()
        {
            var list = new List<string>() { "no PWA" };
            list.AddRange(edgePwaApps.Select(a => a.Name));
            return list;
        }

        internal static List<string> GetChromeAppsList()
        {
            var list = new List<string>() { "no PWA" };
            list.AddRange(chromePwaApps.Select(a => a.Name));
            return list;
        }

        internal static string GetChromeAppIconFile(string pwaAppId)
        {
            var candidates = chromePwaApps.Where(x => x.AppId == pwaAppId).ToList();
            if (candidates.Count > 0)
            {
                return candidates.First().IconFilename;
            }

            return null;
        }

        internal static string GetEdgeAppIconFile(string pwaAppId)
        {
            var candidates = edgePwaApps.Where(x => x.AppId == pwaAppId).ToList();
            if (candidates.Count > 0)
            {
                return candidates.First().IconFilename;
            }

            return null;
        }

        internal static string GetPwaAppId(string pwaAppName)
        {
            var candidates = edgePwaApps.Where(x => x.Name == pwaAppName).ToList();
            if (candidates.Count > 0)
            {
                return candidates.First().AppId;
            }

            candidates = chromePwaApps.Where(x => x.Name == pwaAppName).ToList();
            if (candidates.Count > 0)
            {
                return candidates.First().AppId;
            }

            return string.Empty;
        }

        internal static int GetEdgeItemIndex(string pwaAppId)
        {
            if (string.IsNullOrEmpty(pwaAppId))
            {
                return 0;
            }

            for (int appIndex = 0; appIndex < edgePwaApps.Count; appIndex++)
            {
                if (edgePwaApps[appIndex].AppId == pwaAppId)
                {
                    return appIndex + 1;
                }
            }

            return 0;
        }

        internal static int GetChromeItemIndex(string pwaAppId)
        {
            if (string.IsNullOrEmpty(pwaAppId))
            {
                return 0;
            }

            for (int appIndex = 0; appIndex < chromePwaApps.Count; appIndex++)
            {
                if (chromePwaApps[appIndex].AppId == pwaAppId)
                {
                    return appIndex + 1;
                }
            }

            return 0;
        }
    }
}
