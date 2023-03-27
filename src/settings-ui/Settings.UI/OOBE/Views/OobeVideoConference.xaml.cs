// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

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
    public sealed partial class OobeVideoConference : Page
    {
        public OobePowerToysModule ViewModel { get; set; }

        public OobeVideoConference()
        {
            this.InitializeComponent();
            ViewModel = new OobePowerToysModule(OobeShellPage.OobeShellHandler.Modules[(int)PowerToysModules.VideoConference]);
            DataContext = ViewModel;
        }

        private void SettingsLaunchButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            if (OobeShellPage.OpenMainWindowCallback != null)
            {
                OobeShellPage.OpenMainWindowCallback(typeof(VideoConferencePage));
            }

            ViewModel.LogOpeningSettingsEvent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            ViewModel.LogOpeningModuleEvent();
            HotkeyMicVidControl.Keys = SettingsRepository<VideoConferenceSettings>.GetInstance(new SettingsUtils()).SettingsConfig.Properties.MuteCameraAndMicrophoneHotkey.Value.GetKeysList();
            HotkeyMicControl.Keys = SettingsRepository<VideoConferenceSettings>.GetInstance(new SettingsUtils()).SettingsConfig.Properties.MuteMicrophoneHotkey.Value.GetKeysList();
            HotkeyPushToTalkControl.Keys = SettingsRepository<VideoConferenceSettings>.GetInstance(new SettingsUtils()).SettingsConfig.Properties.PushToTalkMicrophoneHotkey.Value.GetKeysList();
            HotkeyVidControl.Keys = SettingsRepository<VideoConferenceSettings>.GetInstance(new SettingsUtils()).SettingsConfig.Properties.MuteCameraHotkey.Value.GetKeysList();
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            ViewModel.LogClosingModuleEvent();
        }
    }
}
