// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Principal;
using System.Threading;
using Windows.Foundation;
using Windows.Management.Deployment;

namespace Microsoft.PowerToys.Run.Plugin.WindowsTerminal.Helpers
{
    public class TerminalQuery : ITerminalQuery
    {
        private readonly PackageManager _packageManager;

        private static ReadOnlyCollection<string> Packages => new List<string>
        {
            "Microsoft.WindowsTerminal",
            "Microsoft.WindowsTerminalPreview",
        }.AsReadOnly();

        // Cache for the package AUMID that we don't have to query the AUMID every time. (On slow systems, each query costs up to 50 ms.)
        private static readonly Dictionary<string, string> _PackageAumidCache = new Dictionary<string, string>();

        private IEnumerable<TerminalPackage> Terminals => GetTerminals();

        public TerminalQuery()
        {
            _packageManager = new PackageManager();
        }

        public IEnumerable<TerminalProfile> GetProfiles()
        {
            var profiles = new List<TerminalProfile>();

            foreach (var terminal in Terminals)
            {
                if (!File.Exists(terminal.SettingsPath))
                {
                    continue;
                }

                var settingsJson = File.ReadAllText(terminal.SettingsPath);
                profiles.AddRange(TerminalHelper.ParseSettings(terminal, settingsJson));
            }

            return profiles.OrderBy(p => p.Name);
        }

        private IEnumerable<TerminalPackage> GetTerminals()
        {
            var user = WindowsIdentity.GetCurrent().User;
            var localAppDataPath = Environment.GetEnvironmentVariable("LOCALAPPDATA");

            foreach (var p in _packageManager.FindPackagesForUser(user.Value).Where(p => Packages.Contains(p.Id.Name)))
            {
                if (!_PackageAumidCache.ContainsKey(p.Id.Name))
                {
                    // When changing the target release to 19041 in the future, we can use <GetAppListEntries()> instead of <GetAppListEntriesAsync()>!
                    var appListEntries = p.GetAppListEntriesAsync();
                    while (appListEntries.Status != AsyncStatus.Completed)
                    {
                        Thread.Sleep(10);
                        Debug.Print($"Sleep 10 sec for AUMID for {p.Id.Name}>");
                    }

                    _PackageAumidCache.Add(p.Id.Name, appListEntries.GetResults().Single().AppUserModelId);
                }

                var aumid = _PackageAumidCache[p.Id.Name];
                var version = new Version(p.Id.Version.Major, p.Id.Version.Minor, p.Id.Version.Build, p.Id.Version.Revision);
                var settingsPath = Path.Combine(localAppDataPath, "Packages", p.Id.FamilyName, "LocalState", "settings.json");
                yield return new TerminalPackage(aumid, version, p.DisplayName, settingsPath, p.Logo.LocalPath);
            }
        }
    }
}
