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
    internal sealed partial class FileLocksmithModuleInterface : IPowerToysModule
    {
        public string Name => "FileLocksmith";

        public bool Enabled => SettingsUtils.Default.GetSettings<GeneralSettings>().Enabled.FileLocksmith;

        public GpoRuleConfigured GpoRuleConfigured => GPOWrapper.GetConfiguredFileLocksmithEnabledValue();

        public void Disable()
        {
            UpdateFileLocksmithRegistrationWin10(false);
        }

        public void Enable()
        {
            UpdateFileLocksmithRegistrationWin10(true);
            if (Environment.OSVersion.Version.Build >= 22000)
            {
                PackageHelper.InstallPackage(Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!, "WinUI3Apps", "FileLocksmithContextMenuPackage.msix"), [], true);
            }
        }

        [LibraryImport("WinUI3Apps/PowerToys.FileLocksmithExt.dll")]
        private static partial void UpdateFileLocksmithRegistrationWin10([MarshalAs(UnmanagedType.Bool)]bool enabled);
    }
}
