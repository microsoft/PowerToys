// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.ViewModels;

namespace Microsoft.PowerToys.Settings.UI.Views
{
    /// <summary>
    /// Settings page for Input Highlighter (keystroke visualization). It currently
    /// rides on the Mouse Highlighter module/settings, so it reuses
    /// <see cref="MouseUtilsViewModel"/> and binds only the keystroke properties.
    /// </summary>
    public sealed partial class InputHighlighterPage : NavigablePage, IRefreshablePage
    {
        private MouseUtilsViewModel ViewModel { get; set; }

        public InputHighlighterPage()
        {
            var settingsUtils = SettingsUtils.Default;
            ViewModel = new MouseUtilsViewModel(
                settingsUtils,
                SettingsRepository<GeneralSettings>.GetInstance(settingsUtils),
                SettingsRepository<FindMyMouseSettings>.GetInstance(settingsUtils),
                SettingsRepository<MouseHighlighterSettings>.GetInstance(settingsUtils),
                SettingsRepository<MouseJumpSettings>.GetInstance(settingsUtils),
                SettingsRepository<MousePointerCrosshairsSettings>.GetInstance(settingsUtils),
                SettingsRepository<CursorWrapSettings>.GetInstance(settingsUtils),
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
