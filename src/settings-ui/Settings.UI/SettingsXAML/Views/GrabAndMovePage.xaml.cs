// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.ViewModels;

namespace Microsoft.PowerToys.Settings.UI.Views
{
    public sealed partial class GrabAndMovePage : NavigablePage, IRefreshablePage
    {
        private GrabAndMoveViewModel ViewModel { get; set; }

        public GrabAndMovePage()
        {
            var moduleSettingsRepository = SettingsRepository<GrabAndMoveSettings>.GetInstance(SettingsUtils.Default);
            ViewModel = new GrabAndMoveViewModel(
                SettingsRepository<GeneralSettings>.GetInstance(SettingsUtils.Default),
                moduleSettingsRepository.SettingsConfig,
                ShellPage.SendDefaultIPCMessage);
            DataContext = ViewModel;
            InitializeComponent();

            Loaded += (s, e) => ViewModel.OnPageLoaded();
        }

        public void RefreshEnabledState()
        {
            ViewModel.RefreshEnabledState();
        }
    }
}
