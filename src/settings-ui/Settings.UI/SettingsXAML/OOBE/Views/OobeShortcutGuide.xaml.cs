// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.OOBE.Enums;
using Microsoft.PowerToys.Settings.UI.OOBE.ViewModel;
using Microsoft.PowerToys.Settings.UI.Views;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace Microsoft.PowerToys.Settings.UI.OOBE.Views
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class OobeShortcutGuide : Page
    {
        public OobePowerToysModule ViewModel { get; set; }

        public OobeShortcutGuide()
        {
            this.InitializeComponent();
            ViewModel = new OobePowerToysModule(OobeShellPage.OobeShellHandler.Modules[(int)PowerToysModules.ShortcutGuide]);
            DataContext = ViewModel;
        }

        private void Start_ShortcutGuide_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            var executablePath = Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "..", @"PowerToys.ShortcutGuide.exe");
            var id = System.Environment.ProcessId.ToString(CultureInfo.InvariantCulture);
            var p = Process.Start(executablePath, id);
            if (p != null)
            {
                p.Close();
            }

            ViewModel.LogRunningModuleEvent();
        }

        private void SettingsLaunchButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            if (OobeShellPage.OpenMainWindowCallback != null)
            {
                OobeShellPage.OpenMainWindowCallback(typeof(ShortcutGuidePage));
            }

            ViewModel.LogOpeningSettingsEvent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            ViewModel.LogOpeningModuleEvent();
            var settingsProperties = SettingsRepository<ShortcutGuideSettings>.GetInstance(new SettingsUtils()).SettingsConfig.Properties;

            if ((bool)settingsProperties.UseLegacyPressWinKeyBehavior.Value)
            {
                HotkeyControl.Keys = new List<object> { 92 };
            }
            else
            {
                HotkeyControl.Keys = settingsProperties.OpenShortcutGuide.GetKeysList();
            }
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            ViewModel.LogClosingModuleEvent();
        }
    }
}
