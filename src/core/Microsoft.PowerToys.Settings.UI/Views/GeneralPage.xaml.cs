// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.PowerToys.Settings.UI.Lib.ViewModels;
using Windows.ApplicationModel.Resources;
using Windows.Data.Json;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

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

            ViewModel = new GeneralViewModel(
                loader.GetString("GeneralSettings_RunningAsAdminText"),
                loader.GetString("GeneralSettings_RunningAsUserText"),
                ShellPage.IsElevated,
                ShellPage.IsUserAnAdmin,
                UpdateUIThemeMethod,
                ShellPage.SendDefaultIPCMessage,
                ShellPage.SendRestartAdminIPCMessage,
                ShellPage.SendCheckForUpdatesIPCMessage);

            ShellPage.ShellHandler.IPCResponseHandleList.Add((JsonObject json) =>
            {
                string version = json.GetNamedString("version");
                if (version != string.Empty)
                {
                    ViewModel.LatestAvailableVersion = "Latest available version: " + version;
                }
            });

            DataContext = ViewModel;
        }

        public int UpdateUIThemeMethod(string themeName)
        {
            switch (themeName)
            {
                case "light":
                    ShellPage.ShellHandler.RequestedTheme = ElementTheme.Light;
                    break;
                case "dark":
                    ShellPage.ShellHandler.RequestedTheme = ElementTheme.Dark;
                    break;
                case "system":
                    ShellPage.ShellHandler.RequestedTheme = ElementTheme.Default;
                    break;
            }

            return 0;
        }
    }
}
