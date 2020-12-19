// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Utilities;
using Microsoft.PowerToys.Settings.UI.Library.ViewModels;
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
        [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Exceptions from the IPC response handler should be caught and logged.")]
        public GeneralPage()
        {
            InitializeComponent();

            // Load string resources
            ResourceLoader loader = ResourceLoader.GetForViewIndependentUse();
            var settingsUtils = new SettingsUtils();

            ViewModel = new GeneralViewModel(
                SettingsRepository<GeneralSettings>.GetInstance(settingsUtils),
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
                try
                {
                    string version = json.GetNamedString("version", string.Empty);
                    bool isLatest = json.GetNamedBoolean("isVersionLatest", false);

                    var str = string.Empty;
                    if (isLatest)
                    {
                        str = ResourceLoader.GetForCurrentView().GetString("GeneralSettings_VersionIsLatest");
                    }
                    else if (!string.IsNullOrEmpty(version))
                    {
                        str = ResourceLoader.GetForCurrentView().GetString("GeneralSettings_NewVersionIsAvailable");
                        if (!string.IsNullOrEmpty(str))
                        {
                            str += ": " + version;
                        }
                    }

                    // Using CurrentCulture since this is user-facing
                    if (!string.IsNullOrEmpty(str))
                    {
                       ViewModel.LatestAvailableVersion = string.Format(CultureInfo.CurrentCulture, str);
                    }

                    string updateStateDate = json.GetNamedString("updateStateDate", string.Empty);
                    if (!string.IsNullOrEmpty(updateStateDate) && long.TryParse(updateStateDate, out var uTCTime))
                    {
                        var localTime = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(uTCTime).ToLocalTime();
                        ViewModel.UpdateCheckedDate = localTime.ToString(CultureInfo.CurrentCulture);
                    }
                }
                catch (Exception e)
                {
                    Logger.LogError("Exception encountered when reading the version.", e);
                }
            });

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

            return 0;
        }
    }
}
