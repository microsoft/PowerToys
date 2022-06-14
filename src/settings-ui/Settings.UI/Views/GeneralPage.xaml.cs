// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics.CodeAnalysis;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Utilities;
using Microsoft.PowerToys.Settings.UI.Library.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.Resources;
using Windows.UI.Core;

namespace Microsoft.PowerToys.Settings.UI.Views
{
    /// <summary>
    /// General Settings Page.
    /// </summary>
    public sealed partial class GeneralPage : Page
    {
        /// <summary>
        /// Gets or sets view model.
        /// </summary>
        public GeneralViewModel ViewModel { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="GeneralPage"/> class.
        /// General Settings page constructor.
        /// </summary>
        public GeneralPage()
        {
            InitializeComponent();

            // Load string resources
            ResourceLoader loader = ResourceLoader.GetForViewIndependentUse();
            var settingsUtils = new SettingsUtils();

            Action stateUpdatingAction = () =>
            {
                this.DispatcherQueue.TryEnqueue(() =>
                {
                    ViewModel.RefreshUpdatingState();
                });
            };

            ViewModel = new GeneralViewModel(
                SettingsRepository<GeneralSettings>.GetInstance(settingsUtils),
                loader.GetString("GeneralSettings_RunningAsAdminText"),
                loader.GetString("GeneralSettings_RunningAsUserText"),
                ShellPage.IsElevated,
                ShellPage.IsUserAnAdmin,
                UpdateUIThemeMethod,
                ShellPage.SendDefaultIPCMessage,
                ShellPage.SendRestartAdminIPCMessage,
                ShellPage.SendCheckForUpdatesIPCMessage,
                string.Empty,
                stateUpdatingAction);

            DataContext = ViewModel;
        }

        public static int UpdateUIThemeMethod(string themeName)
        {
            switch (themeName?.ToUpperInvariant())
            {
                case "LIGHT":
                    ShellPage.ShellHandler.RequestedTheme = ElementTheme.Light;
                    break;
                case "DARK":
                    ShellPage.ShellHandler.RequestedTheme = ElementTheme.Dark;
                    break;
                case "SYSTEM":
                    ShellPage.ShellHandler.RequestedTheme = ElementTheme.Default;
                    break;
                default:
                    Logger.LogError($"Unexpected theme name: {themeName}");
                    break;
            }

            App.HandleThemeChange();
            return 0;
        }

        private void OpenColorsSettings_Click(object sender, RoutedEventArgs e)
        {
            Helpers.StartProcessHelper.Start(Helpers.StartProcessHelper.ColorsSettings);
        }
    }
}
