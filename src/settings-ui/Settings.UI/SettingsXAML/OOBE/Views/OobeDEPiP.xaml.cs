// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading;

using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Helpers;
using Microsoft.PowerToys.Settings.UI.OOBE.Enums;
using Microsoft.PowerToys.Settings.UI.OOBE.ViewModel;
using Microsoft.PowerToys.Settings.UI.Views;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using PowerToys.Interop;

namespace Microsoft.PowerToys.Settings.UI.OOBE.Views
{
    public sealed partial class OobeDEPiP : Page
    {
        public OobePowerToysModule ViewModel { get; }

        public OobeDEPiP()
        {
            InitializeComponent();
            ViewModel = App.OobeShellViewModel.GetModule(PowerToysModules.DEPiP);
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            ViewModel.LogOpeningModuleEvent();

            var generalSettings = SettingsRepository<GeneralSettings>.GetInstance(SettingsUtils.Default).SettingsConfig;
            LaunchButton.IsEnabled = ModuleHelper.GetIsModuleEnabled(generalSettings, ManagedCommon.ModuleType.DEPiP);
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            ViewModel.LogClosingModuleEvent();
        }

        private void Launch_DEPiP_Click(object sender, RoutedEventArgs e)
        {
            if (App.PowerToysPID != 0)
            {
                NativeMethods.AllowSetForegroundWindow(App.PowerToysPID);
            }

            using var eventHandle = new EventWaitHandle(false, EventResetMode.AutoReset, Constants.ShowDEPiPSharedEvent());
            eventHandle.Set();
        }

        private void SettingsLaunchButton_Click(object sender, RoutedEventArgs e)
        {
            OobeWindow.OpenMainWindowCallback?.Invoke(typeof(DEPiPPage));
            ViewModel.LogOpeningSettingsEvent();
        }
    }
}
