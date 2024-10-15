// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Win32;

namespace WorkspacesEditor.Utils
{
    public class PwaHelper
    {
        private const string EdgeBase = "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\AppModel\\StateRepository\\Cache\\Application\\Data";
        private const string EdgePwaIdentifier = "PWA";
        private const string EdgeApplicationUserModelId = "ApplicationUserModelId";
        private const string EdgeHostId = "HostId";
        private const string EdgeParameters = "Parameters";
        private const string ChromeBase = "Google\\Chrome\\User Data\\Default\\Web Applications";
        private const string ChromeIdentifier = "_crx_";

        private static List<PwaApp> edgePwaApps = new List<PwaApp>();
        private static List<PwaApp> chromePwaApps = new List<PwaApp>();

        public static int EdgeAppsCount { get => edgePwaApps.Count; }

        public static int ChromeAppsCount { get => chromePwaApps.Count; }

        public PwaHelper()
        {
            InitEdgePwaData();
            InitChromePwaData();
        }

        private void InitEdgePwaData()
        {
            try
            {
                using (RegistryKey key = Registry.LocalMachine.OpenSubKey(EdgeBase))
                {
                    if (key != null)
                    {
                        foreach (string subKeyDir in key.GetSubKeyNames())
                        {
                            using (RegistryKey subKey = Registry.LocalMachine.OpenSubKey(EdgeBase + '\\' + subKeyDir))
                            {
                                if (subKey == null)
                                {
                                    continue;
                                }

                                string hostId = GetValueFromKey(subKey, EdgeHostId);
                                if (hostId == null)
                                {
                                    continue;
                                }

                                if (hostId != EdgePwaIdentifier)
                                {
                                    continue;
                                }

                                string applicationUserModelId = GetValueFromKey(subKey, EdgeApplicationUserModelId);
                                if (applicationUserModelId == null)
                                {
                                    continue;
                                }

                                string appName = applicationUserModelId.Split('_').First();
                                string parameters = GetValueFromKey(subKey, EdgeParameters);
                                if (parameters == null)
                                {
                                    continue;
                                }

                                if (edgePwaApps.Any(x => x.Name == appName))
                                {
                                    continue;
                                }

                                edgePwaApps.Add(new PwaApp() { Name = appName, Parameters = parameters });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        private string GetValueFromKey(RegistryKey p_key, string p_valueName)
        {
            object value = p_key.GetValue(p_valueName);
            if (value == null)
            {
                return null;
            }

            if (!(value is string))
            {
                return null;
            }

            return value as string;
        }

        private void InitChromePwaData()
        {
            var baseFolderName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), ChromeBase);
            if (Directory.Exists(baseFolderName))
            {
                foreach (string subDir in Directory.GetDirectories(baseFolderName))
                {
                    string dirName = Path.GetFileName(subDir);
                    if (!dirName.StartsWith(ChromeIdentifier, StringComparison.InvariantCulture))
                    {
                        continue;
                    }

                    foreach (string file in Directory.GetFiles(subDir, "*.lnk"))
                    {
                        string filenameWithoutExtension = Path.GetFileNameWithoutExtension(file);
                        chromePwaApps.Add(new PwaApp() { Name = filenameWithoutExtension, Parameters = file });
                    }
                }
            }
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
    }
}
