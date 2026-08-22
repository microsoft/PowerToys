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
    internal sealed partial class ImageResizerModuleInterface : IPowerToysModule
    {
        public string Name => "ImageResizer";

        public bool Enabled => SettingsUtils.Default.GetSettings<GeneralSettings>().Enabled.ImageResizer;

        public GpoRuleConfigured GpoRuleConfigured => GPOWrapper.GetConfiguredImageResizerEnabledValue();

        public void Disable()
        {
            UpdateImageResizerRegistrationWin10(false);
        }

        public void Enable()
        {
            UpdateImageResizerRegistrationWin10(true);
            AIHelper.DetectAiCapabilities(true);
            if (Environment.OSVersion.Version.Build >= 22000)
            {
                PackageHelper.InstallPackage(Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!, "WinUI3Apps", "ImageResizerContextMenuPackage.msix"), [], true);
            }
        }

        [LibraryImport("WinUI3Apps/PowerToys.ImageResizerExt.dll")]
        private static partial void UpdateImageResizerRegistrationWin10([MarshalAs(UnmanagedType.Bool)]bool enabled);
    }
}
