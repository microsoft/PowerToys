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
using Windows.Management.Deployment;

namespace RunnerV2.ModuleInterfaces
{
    internal sealed partial class NewPlusModuleInterface : IPowerToysModule
    {
        public string Name => "NewPlus";

        public bool Enabled => SettingsUtils.Default.GetSettings<GeneralSettings>().Enabled.NewPlus;

        public GpoRuleConfigured GpoRuleConfigured => GPOWrapper.GetConfiguredNewPlusEnabledValue();

        public void Disable()
        {
            UpdateNewPlusRegistrationWin10(false);
        }

        public void Enable()
        {
            UpdateNewPlusRegistrationWin10(true);
            if (Environment.OSVersion.Version.Build >= 22000)
            {
                PackageHelper.InstallPackage(Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!, "WinUI3Apps", "NewPlusPackage.msix"), [], true);
            }
        }

        [LibraryImport("WinUI3Apps/PowerToys.NewPlus.ShellExtension.dll")]
        private static partial void UpdateNewPlusRegistrationWin10([MarshalAs(UnmanagedType.Bool)]bool enabled);
    }
}
