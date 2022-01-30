// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Principal;
using Windows.Management.Deployment;

namespace Microsoft.PowerToys.Run.Plugin.WindowsTerminal.Helpers
{
    public class TerminalQuery : ITerminalQuery
    {
        /// Static list of all Windows Terminal packages. As key we use the app name and in the value we save the AUMID of each package.
        /// AUMID = ApplicationUserModelId: This is an identifier id for the app. The syntax is '<PackageFamilyName>!App'.
        /// The AUMID of an AppX package will never change. (https://github.com/microsoft/PowerToys/pull/15836#issuecomment-1025204301)
        private static readonly IReadOnlyDictionary<string, string> Packages = new Dictionary<string, string>()
        {
            { "Microsoft.WindowsTerminal", "Microsoft.WindowsTerminal_8wekyb3d8bbwe!App" },
            { "Microsoft.WindowsTerminalPreview", "Microsoft.WindowsTerminalPreview_8wekyb3d8bbwe!App" },
        };

        private readonly PackageManager _packageManager;

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

            foreach (var p in _packageManager.FindPackagesForUser(user.Value).Where(p => Packages.Keys.Contains(p.Id.Name)))
            {
                var aumid = Packages[p.Id.Name];
                var version = new Version(p.Id.Version.Major, p.Id.Version.Minor, p.Id.Version.Build, p.Id.Version.Revision);
                var settingsPath = Path.Combine(localAppDataPath, "Packages", p.Id.FamilyName, "LocalState", "settings.json");
                yield return new TerminalPackage(aumid, version, p.DisplayName, settingsPath, p.Logo.LocalPath);
            }
       }
    }
}
