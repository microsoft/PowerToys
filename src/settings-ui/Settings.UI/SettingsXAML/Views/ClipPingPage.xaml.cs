// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading;
using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.ViewModels;
using Microsoft.UI.Xaml.Controls;
using PowerToys.Interop;

namespace Microsoft.PowerToys.Settings.UI.Views
{
    public sealed partial class ClipPingPage : Page, IRefreshablePage
    {
        private ClipPingViewModel ViewModel { get; }

        public ClipPingPage()
        {
            var settingsUtils = new SettingsUtils();
            ViewModel = new ClipPingViewModel(
                settingsUtils,
                SettingsRepository<GeneralSettings>.GetInstance(settingsUtils),
                SettingsRepository<ClipPingSettings>.GetInstance(settingsUtils),
                ShellPage.SendDefaultIPCMessage);
            DataContext = ViewModel;
            InitializeComponent();
        }

        private void TestOverlayClick(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            using var eventHandle = new EventWaitHandle(false, EventResetMode.AutoReset, Constants.ClipPingShowOverlayEvent());
            eventHandle.Set();
        }

        public void RefreshEnabledState()
        {
            ViewModel.RefreshEnabledState();
        }
    }
}
