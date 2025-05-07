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
    public sealed partial class CmdPalPage : Page, IRefreshablePage
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
            InitializeComponent();
        }

        public void RefreshEnabledState()
        {
            ViewModel.RefreshEnabledState();
        }

        private void LaunchApp(string appPath)
        {
            try
            {
                string dir = Path.GetDirectoryName(appPath);

                var processStartInfo = new ProcessStartInfo
                {
                    FileName = appPath,
                    Arguments = string.Empty,
                    WorkingDirectory = dir,
                    UseShellExecute = true,
                    Verb = "open",
                    CreateNoWindow = false,
                };

                Process process = Process.Start(processStartInfo) ?? throw new InvalidOperationException("Failed to start the process.");
                process.WaitForInputIdle();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to launch CmdPal settings: {ex.Message}");
            }
        }

        private void CmdPalSettingsDeeplink_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            // Launch CmdPal settings window
            string launchPath = "x-cmdpal://settings";
            LaunchApp(launchPath);
        }
    }
}
