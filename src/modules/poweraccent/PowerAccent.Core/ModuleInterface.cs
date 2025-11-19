// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Globalization;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library;
using PowerToys.GPOWrapper;

namespace PowerAccent.Core
{
    internal sealed class ModuleInterface : IPowerToysModule
    {
        public string Name => "PowerAccent";

        public bool Enabled => new SettingsUtils().GetSettingsOrDefault<GeneralSettings>().Enabled.PowerAccent;

        public GpoRuleConfigured GpoRuleConfigured => GPOWrapper.GetConfiguredQuickAccentEnabledValue();

        public void Disable()
        {
            foreach (var process in Process.GetProcessesByName("PowerToys.PowerAccent.exe"))
            {
                process.Kill();
            }
        }

        public void Enable()
        {
            Disable();

            Process.Start("PowerToys.PowerAccent.exe", Environment.ProcessId.ToString(CultureInfo.InvariantCulture));
        }
    }
}
