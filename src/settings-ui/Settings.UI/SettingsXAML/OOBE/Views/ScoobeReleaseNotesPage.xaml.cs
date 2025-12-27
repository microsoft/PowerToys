// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using System.Text.RegularExpressions;

using global::PowerToys.GPOWrapper;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.HotkeyConflicts;
using Microsoft.PowerToys.Settings.UI.OOBE.ViewModel;
using Microsoft.PowerToys.Settings.UI.Services;
using Microsoft.PowerToys.Settings.UI.Views;
using Microsoft.PowerToys.Telemetry;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace Microsoft.PowerToys.Settings.UI.OOBE.Views
{
    public sealed partial class ScoobeReleaseNotesPage : Page, INotifyPropertyChanged
    {
        public OobePowerToysModule ViewModel { get; set; }

        private AllHotkeyConflictsData _allHotkeyConflictsData = new AllHotkeyConflictsData();

        public bool ShowDataDiagnosticsInfoBar => GetShowDataDiagnosticsInfoBar();

        private int _conflictCount;

        private IList<PowerToysReleaseInfo> _currentReleases;

        public AllHotkeyConflictsData AllHotkeyConflictsData
        {
            get => _allHotkeyConflictsData;
            set
            {
                if (_allHotkeyConflictsData != value)
                {
                    _allHotkeyConflictsData = value;

                    UpdateConflictCount();

                    OnPropertyChanged(nameof(AllHotkeyConflictsData));
                    OnPropertyChanged(nameof(HasConflicts));
                }
            }
        }

        public bool HasConflicts => _conflictCount > 0;

        private void UpdateConflictCount()
        {
            int count = 0;
            if (AllHotkeyConflictsData == null)
            {
                _conflictCount = count;
            }

            if (AllHotkeyConflictsData.InAppConflicts != null)
            {
                foreach (var inAppConflict in AllHotkeyConflictsData.InAppConflicts)
                {
                    if (!inAppConflict.ConflictIgnored)
                    {
                        count++;
                    }
                }
            }

            if (AllHotkeyConflictsData.SystemConflicts != null)
            {
                foreach (var systemConflict in AllHotkeyConflictsData.SystemConflicts)
                {
                    if (!systemConflict.ConflictIgnored)
                    {
                        count++;
                    }
                }
            }

            _conflictCount = count;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Initializes a new instance of the <see cref="ScoobeReleaseNotesPage"/> class.
        /// </summary>
        public ScoobeReleaseNotesPage()
        {
            this.InitializeComponent();

            // Subscribe to hotkey conflict updates
            if (GlobalHotkeyConflictManager.Instance != null)
            {
                GlobalHotkeyConflictManager.Instance.ConflictsUpdated += OnConflictsUpdated;
                GlobalHotkeyConflictManager.Instance.RequestAllConflicts();
            }
        }

        private void OnConflictsUpdated(object sender, AllHotkeyConflictsEventArgs e)
        {
            this.DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Normal, () =>
            {
                var allConflictData = e.Conflicts;
                foreach (var inAppConflict in allConflictData.InAppConflicts)
                {
                    var hotkey = inAppConflict.Hotkey;
                    var hotkeySetting = new HotkeySettings(hotkey.Win, hotkey.Ctrl, hotkey.Alt, hotkey.Shift, hotkey.Key);
                    inAppConflict.ConflictIgnored = HotkeyConflictIgnoreHelper.IsIgnoringConflicts(hotkeySetting);
                }

                foreach (var systemConflict in allConflictData.SystemConflicts)
                {
                    var hotkey = systemConflict.Hotkey;
                    var hotkeySetting = new HotkeySettings(hotkey.Win, hotkey.Ctrl, hotkey.Alt, hotkey.Shift, hotkey.Key);
                    systemConflict.ConflictIgnored = HotkeyConflictIgnoreHelper.IsIgnoringConflicts(hotkeySetting);
                }

                AllHotkeyConflictsData = e.Conflicts ?? new AllHotkeyConflictsData();
            });
        }

        private void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private bool GetShowDataDiagnosticsInfoBar()
        {
            var isDataDiagnosticsGpoDisallowed = GPOWrapper.GetAllowDataDiagnosticsValue() == GpoRuleConfigured.Disabled;

            if (isDataDiagnosticsGpoDisallowed)
            {
                return false;
            }

            bool userActed = DataDiagnosticsSettings.GetUserActionValue();

            if (userActed)
            {
                return false;
            }

            bool registryValue = DataDiagnosticsSettings.GetEnabledValue();

            bool isFirstRunAfterUpdate = (App.Current as App).ShowScoobe;
            if (isFirstRunAfterUpdate && registryValue == false)
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Regex to remove installer hash sections from the release notes.
        /// </summary>
        private const string RemoveInstallerHashesRegex = @"(\r\n)+## Installer Hashes(\r\n.*)+## Highlights";
        private const string RemoveHotFixInstallerHashesRegex = @"(\r\n)+## Installer Hashes(\r\n.*)+$";
        private const RegexOptions RemoveInstallerHashesRegexOptions = RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;

        private static string GetReleaseNotesMarkdown(IList<PowerToysReleaseInfo> releases)
        {
            if (releases == null || releases.Count == 0)
            {
                return string.Empty;
            }

            StringBuilder releaseNotesHtmlBuilder = new StringBuilder(string.Empty);

            // Regex to remove installer hash sections from the release notes.
            Regex removeHashRegex = new Regex(RemoveInstallerHashesRegex, RemoveInstallerHashesRegexOptions);

            // Regex to remove installer hash sections from the release notes, since there'll be no Highlights section for hotfix releases.
            Regex removeHotfixHashRegex = new Regex(RemoveHotFixInstallerHashesRegex, RemoveInstallerHashesRegexOptions);

            int counter = 0;
            foreach (var release in releases)
            {
                releaseNotesHtmlBuilder.AppendLine("# " + release.Name);
                var notes = removeHashRegex.Replace(release.ReleaseNotes, "\r\n### Highlights");
                notes = notes.Replace("[github-current-release-work]", $"[github-current-release-work{++counter}]");
                notes = removeHotfixHashRegex.Replace(notes, string.Empty);
                releaseNotesHtmlBuilder.AppendLine(notes);
                releaseNotesHtmlBuilder.AppendLine("&nbsp;");
            }

            return releaseNotesHtmlBuilder.ToString();
        }

        private void DisplayReleaseNotes()
        {
            if (_currentReleases == null || _currentReleases.Count == 0)
            {
                ReleaseNotesMarkdown.Visibility = Visibility.Collapsed;
                ErrorInfoBar.IsOpen = true;
                return;
            }

            try
            {
                LoadingProgressRing.Visibility = Visibility.Collapsed;
                ProxyWarningInfoBar.IsOpen = false;
                ErrorInfoBar.IsOpen = false;

                string releaseNotesMarkdown = GetReleaseNotesMarkdown(_currentReleases);
                ReleaseNotesMarkdown.Text = releaseNotesMarkdown;
                ReleaseNotesMarkdown.Visibility = Visibility.Visible;
            }
            catch (Exception ex)
            {
                Logger.LogError("Exception when displaying the release notes", ex);
                ErrorInfoBar.IsOpen = true;
            }
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            DisplayReleaseNotes();
        }

        /// <inheritdoc/>
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            if (e.Parameter is IList<PowerToysReleaseInfo> releases)
            {
                _currentReleases = releases;
            }

            ViewModel.LogOpeningModuleEvent();
        }

        /// <inheritdoc/>
        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            ViewModel.LogClosingModuleEvent();

            // Unsubscribe from conflict updates when leaving the page
            if (GlobalHotkeyConflictManager.Instance != null)
            {
                GlobalHotkeyConflictManager.Instance.ConflictsUpdated -= OnConflictsUpdated;
            }
        }

        private void DataDiagnostics_InfoBar_YesNo_Click(object sender, RoutedEventArgs e)
        {
            string commandArg = string.Empty;
            if (sender is Button senderBtn)
            {
                commandArg = senderBtn.CommandParameter.ToString();
            }
            else if (sender is HyperlinkButton senderLink)
            {
                commandArg = senderLink.CommandParameter.ToString();
            }

            if (string.IsNullOrEmpty(commandArg))
            {
                return;
            }

            // Update UI
            if (commandArg == "Yes")
            {
                WhatsNewDataDiagnosticsInfoBar.Header = ResourceLoaderInstance.ResourceLoader.GetString("Oobe_WhatsNew_DataDiagnostics_Yes_Click_InfoBar_Title");
            }
            else
            {
                WhatsNewDataDiagnosticsInfoBar.Header = ResourceLoaderInstance.ResourceLoader.GetString("Oobe_WhatsNew_DataDiagnostics_No_Click_InfoBar_Title");
            }

            WhatsNewDataDiagnosticsInfoBarDescText.Visibility = Visibility.Collapsed;
            WhatsNewDataDiagnosticsInfoBarDescTextYesClicked.Visibility = Visibility.Visible;
            DataDiagnosticsButtonYes.Visibility = Visibility.Collapsed;
            DataDiagnosticsButtonNo.Visibility = Visibility.Collapsed;

            // Set Data Diagnostics registry values
            if (commandArg == "Yes")
            {
                DataDiagnosticsSettings.SetEnabledValue(true);
            }
            else
            {
                DataDiagnosticsSettings.SetEnabledValue(false);
            }

            DataDiagnosticsSettings.SetUserActionValue(true);

            this.DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Normal, () =>
            {
                ShellPage.ShellHandler?.SignalGeneralDataUpdate();
            });
        }

        private void DataDiagnostics_InfoBar_Close_Click(object sender, RoutedEventArgs e)
        {
            WhatsNewDataDiagnosticsInfoBar.Visibility = Visibility.Collapsed;
        }

        private void DataDiagnostics_OpenSettings_Click(Microsoft.UI.Xaml.Documents.Hyperlink sender, Microsoft.UI.Xaml.Documents.HyperlinkClickEventArgs args)
        {
            Common.UI.SettingsDeepLink.OpenSettings(Common.UI.SettingsDeepLink.SettingsWindow.Overview);
        }

        private void LoadReleaseNotes_Click(object sender, RoutedEventArgs e)
        {
            DisplayReleaseNotes();
        }
    }
}
