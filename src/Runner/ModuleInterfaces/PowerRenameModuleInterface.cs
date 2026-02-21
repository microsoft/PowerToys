// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.PowerToys.Settings.UI.Library;
using PowerToys.GPOWrapper;
using RunnerV2.Helpers;
using RunnerV2.Models;

namespace RunnerV2.ModuleInterfaces
{
    internal sealed partial class PowerRenameModuleInterface : IPowerToysModule
    {
        public string Name => "PowerRename";

        public bool Enabled => SettingsUtils.Default.GetSettings<GeneralSettings>().Enabled.PowerRename;

        public GpoRuleConfigured GpoRuleConfigured => GPOWrapper.GetConfiguredPowerRenameEnabledValue();

        public void Disable()
        {
            UpdatePowerRenameRegistrationWin10(false);
        }

        public void Enable()
        {
            UpdatePowerRenameRegistrationWin10(true);
            if (Environment.OSVersion.Version.Build >= 22000)
            {
                PackageHelper.InstallPackage(Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!, "WinUI3Apps", "PowerRenameContextMenuPackage.msix"), [], true);
            }
        }

        [LibraryImport("WinUI3Apps/PowerToys.PowerRenameExt.dll")]
        private static partial void UpdatePowerRenameRegistrationWin10([MarshalAs(UnmanagedType.Bool)]bool enabled);
    }
}
