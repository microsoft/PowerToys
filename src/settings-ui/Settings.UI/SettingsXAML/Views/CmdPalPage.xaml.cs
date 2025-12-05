// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.IO;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.ViewModels;
using Microsoft.UI.Xaml.Controls;

namespace Microsoft.PowerToys.Settings.UI.Views
{
    public sealed partial class CmdPalPage : NavigablePage, IRefreshablePage
    {
        private CmdPalViewModel ViewModel { get; set; }

        public CmdPalPage()
        {
            var settingsUtils = new SettingsUtils();
            ViewModel = new CmdPalViewModel(
                settingsUtils,
                SettingsRepository<GeneralSettings>.GetInstance(settingsUtils),
                ShellPage.SendDefaultIPCMessage,
                DispatcherQueue);
            DataContext = ViewModel;
            Loaded += (s, e) => ViewModel.OnPageLoaded();
            InitializeComponent();
        }

        public void RefreshEnabledState()
        {
            ViewModel.RefreshEnabledState();
        }

        private void LaunchApp(string appPath, string args)
        {
            try
            {
                string dir = Path.GetDirectoryName(appPath);

                var processStartInfo = new ProcessStartInfo
                {
                    FileName = appPath,
                    Arguments = args,
                    WorkingDirectory = dir,
                    UseShellExecute = true,
                    Verb = "open",
                    CreateNoWindow = false,
                };

                Process process = Process.Start(processStartInfo);
                if (process == null)
                {
                    Logger.LogError($"Failed to launch CmdPal settings page.");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to launch CmdPal settings: {ex.Message}");
            }
        }

        private void SettingsCard_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            // Launch CmdPal settings window as normal user using explorer
            string launchPath = "explorer.exe";
            string launchArgs = "x-cmdpal://settings";
            LaunchApp(launchPath, launchArgs);
        }

        private void LaunchCard_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            // Launch CmdPal window as normal user using explorer
            string launchPath = "explorer.exe";
            string launchArgs = "x-cmdpal:";
            LaunchApp(launchPath, launchArgs);
        }
    }
}
