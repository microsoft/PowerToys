// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading;
using Microsoft.PowerToys.Settings.UI.OOBE.Enums;
using Microsoft.PowerToys.Settings.UI.OOBE.ViewModel;
using Microsoft.PowerToys.Settings.UI.Views;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

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
            ViewModel = new OobePowerToysModule(OobeShellPage.OobeShellHandler.Modules[(int)PowerToysModulesEnum.ShortcutGuide]);
            DataContext = ViewModel;
        }

        private void Start_ShortcutGuide_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            var executablePath = Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, @"..\modules\ShortcutGuide\ShortcutGuide\PowerToys.ShortcutGuide.exe");
            var id = Process.GetCurrentProcess().Id.ToString(CultureInfo.InvariantCulture);
            var p = Process.Start(executablePath, id);
            if (p != null)
            {
                p.Close();
            }

            ViewModel.LogRunningModuleEvent();
        }

        private void SettingsLaunchButton_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
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
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            ViewModel.LogClosingModuleEvent();
        }
    }
}
