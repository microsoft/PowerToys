// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows.Input;

using global::PowerToys.GPOWrapper;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Helpers;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;
using Microsoft.PowerToys.Settings.UI.Library.ViewModels.Commands;
using Microsoft.PowerToys.Settings.UI.SerializationContext;

namespace Microsoft.PowerToys.Settings.UI.ViewModels
{
    public partial class PowerLauncherViewModel : Observable
    {
        private int _themeIndex;
        private int _monitorPositionIndex;

        private GpoRuleConfigured _enabledGpoRuleConfiguration;
        private bool _enabledStateIsGPOConfigured;
        private bool _isEnabled;
        private string _searchText;

        private GeneralSettings GeneralSettingsConfig { get; set; }

        private PowerLauncherSettings settings;

        public delegate void SendCallback(PowerLauncherSettings settings);

        private readonly SendCallback callback;

        private readonly Func<bool> isDark;

        private Func<string, int> SendConfigMSG { get; }

        public PowerLauncherViewModel(
            PowerLauncherSettings settings,
            ISettingsRepository<GeneralSettings> settingsRepository,
            Func<string, int> ipcMSGCallBackFunc,
            Func<bool> isDark)
        {
            if (settings == null)
            {
                throw new ArgumentException("settings argument cannot be null");
            }

            this.settings = settings;
            this.isDark = isDark;

            // To obtain the general Settings configurations of PowerToys
            ArgumentNullException.ThrowIfNull(settingsRepository);

            GeneralSettingsConfig = settingsRepository.SettingsConfig;

            InitializeEnabledValue();

            // set the callback functions value to handle outgoing IPC message.
            SendConfigMSG = ipcMSGCallBackFunc;
            callback = (PowerLauncherSettings s) =>
            {
                // Propagate changes to Power Launcher through IPC
                // Using InvariantCulture as this is an IPC message
                SendConfigMSG(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "{{ \"powertoys\": {{ \"{0}\": {1} }} }}",
                        PowerLauncherSettings.ModuleName,
                        JsonSerializer.Serialize(s, SourceGenerationContextContext.Default.PowerLauncherSettings)));
            };

            switch (settings.Properties.Theme)
            {
                case Theme.Dark:
                    _themeIndex = 0;
                    break;
                case Theme.Light:
                    _themeIndex = 1;
                    break;
                case Theme.System:
                    _themeIndex = 2;
                    break;
            }

            switch (settings.Properties.Position)
            {
                case StartupPosition.Cursor:
                    _monitorPositionIndex = 0;
                    break;
                case StartupPosition.PrimaryMonitor:
                    _monitorPositionIndex = 1;
                    break;
                case StartupPosition.Focus:
                    _monitorPositionIndex = 2;
                    break;
            }

            SearchPluginsCommand = new Library.ViewModels.Commands.RelayCommand(SearchPlugins);
        }

        private void InitializeEnabledValue()
        {
            _enabledGpoRuleConfiguration = GPOWrapper.GetConfiguredPowerLauncherEnabledValue();
            if (_enabledGpoRuleConfiguration == GpoRuleConfigured.Disabled || _enabledGpoRuleConfiguration == GpoRuleConfigured.Enabled)
            {
                // Get the enabled state from GPO.
                _enabledStateIsGPOConfigured = true;
                _isEnabled = _enabledGpoRuleConfiguration == GpoRuleConfigured.Enabled;
            }
            else
            {
                _isEnabled = GeneralSettingsConfig.Enabled.PowerLauncher;
            }
        }

        private void OnPluginInfoChange(object sender, PropertyChangedEventArgs e)
        {
            if (
                e.PropertyName == nameof(PowerLauncherPluginViewModel.ShowNotAccessibleWarning)
                || e.PropertyName == nameof(PowerLauncherPluginViewModel.ShowBadgeOnPluginSettingError)
                )
            {
                // Don't trigger a settings update if the changed property is for visual notification.
                return;
            }

            OnPropertyChanged(nameof(ShowAllPluginsDisabledWarning));
            OnPropertyChanged(nameof(ShowPluginsAreGpoManagedInfo));
            UpdateSettings();
        }

        public PowerLauncherViewModel(PowerLauncherSettings settings, SendCallback callback)
        {
            this.settings = settings;
            this.callback = callback;
        }

        private void UpdateSettings([CallerMemberName] string propertyName = null)
        {
            // Notify UI of property change
            OnPropertyChanged(propertyName);

            callback(settings);
        }

        public bool EnablePowerLauncher
        {
            get
            {
                return _isEnabled;
            }

            set
            {
                if (_enabledStateIsGPOConfigured)
                {
                    // If it's GPO configured, shouldn't be able to change this state.
                    return;
                }

                if (_isEnabled != value)
                {
                    _isEnabled = value;
                    GeneralSettingsConfig.Enabled.PowerLauncher = value;
                    OnPropertyChanged(nameof(EnablePowerLauncher));
                    OnPropertyChanged(nameof(ShowAllPluginsDisabledWarning));
                    OnPropertyChanged(nameof(ShowPluginsLoadingMessage));
                    OnPropertyChanged(nameof(ShowPluginsAreGpoManagedInfo));
                    OutGoingGeneralSettings outgoing = new OutGoingGeneralSettings(GeneralSettingsConfig);
                    SendConfigMSG(outgoing.ToString());
                }
            }
        }

        public void RefreshEnabledState()
        {
            InitializeEnabledValue();
            OnPropertyChanged(nameof(EnablePowerLauncher));
            OnPropertyChanged(nameof(ShowAllPluginsDisabledWarning));
            OnPropertyChanged(nameof(ShowPluginsLoadingMessage));
            OnPropertyChanged(nameof(ShowPluginsAreGpoManagedInfo));
        }

        public bool IsEnabledGpoConfigured
        {
            get => _enabledStateIsGPOConfigured;
        }

        public string SearchResultPreference
        {
            get
            {
                return settings.Properties.SearchResultPreference;
            }

            set
            {
                if (settings.Properties.SearchResultPreference != value)
                {
                    settings.Properties.SearchResultPreference = value;
                    UpdateSettings();
                }
            }
        }

        public string SearchTypePreference
        {
            get
            {
                return settings.Properties.SearchTypePreference;
            }

            set
            {
                if (settings.Properties.SearchTypePreference != value)
                {
                    settings.Properties.SearchTypePreference = value;
                    UpdateSettings();
                }
            }
        }

        public int MaximumNumberOfResults
        {
            get
            {
                return settings.Properties.MaximumNumberOfResults;
            }

            set
            {
                if (settings.Properties.MaximumNumberOfResults != value)
                {
                    settings.Properties.MaximumNumberOfResults = value;
                    UpdateSettings();
                }
            }
        }

        public int ThemeIndex
        {
            get
            {
                return _themeIndex;
            }

            set
            {
                switch (value)
                {
                    case 0: settings.Properties.Theme = Theme.Dark; break;
                    case 1: settings.Properties.Theme = Theme.Light; break;
                    case 2: settings.Properties.Theme = Theme.System; break;
                }

                _themeIndex = value;
                UpdateSettings();
            }
        }

        public int MonitorPositionIndex
        {
            get
            {
                return _monitorPositionIndex;
            }

            set
            {
                if (_monitorPositionIndex != value)
                {
                    switch (value)
                    {
                        case 0: settings.Properties.Position = StartupPosition.Cursor; break;
                        case 1: settings.Properties.Position = StartupPosition.PrimaryMonitor; break;
                        case 2: settings.Properties.Position = StartupPosition.Focus; break;
                    }

                    _monitorPositionIndex = value;
                    UpdateSettings();
                }
            }
        }

        public string SearchText
        {
            get
            {
                return _searchText;
            }

            set
            {
                if (_searchText != value)
                {
                    _searchText = value;
                    OnPropertyChanged(nameof(SearchText));
                }
            }
        }

        public ICommand SearchPluginsCommand { get; }

        public HotkeySettings OpenPowerLauncher
        {
            get
            {
                return settings.Properties.OpenPowerLauncher;
            }

            set
            {
                if (settings.Properties.OpenPowerLauncher != value)
                {
                    settings.Properties.OpenPowerLauncher = value ?? settings.Properties.DefaultOpenPowerLauncher;
                    UpdateSettings();
                }
            }
        }

        public bool UseCentralizedKeyboardHook
        {
            get
            {
                return settings.Properties.UseCentralizedKeyboardHook;
            }

            set
            {
                if (settings.Properties.UseCentralizedKeyboardHook != value)
                {
                    settings.Properties.UseCentralizedKeyboardHook = value;
                    UpdateSettings();
                }
            }
        }

        public bool SearchQueryResultsWithDelay
        {
            get
            {
                return settings.Properties.SearchQueryResultsWithDelay;
            }

            set
            {
                if (settings.Properties.SearchQueryResultsWithDelay != value)
                {
                    settings.Properties.SearchQueryResultsWithDelay = value;
                    UpdateSettings();
                }
            }
        }

        public int SearchInputDelayFast
        {
            get
            {
                return settings.Properties.SearchInputDelayFast;
            }

            set
            {
                if (settings.Properties.SearchInputDelayFast != value)
                {
                    settings.Properties.SearchInputDelayFast = value;
                    UpdateSettings();
                }
            }
        }

        public int SearchInputDelay
        {
            get
            {
                return settings.Properties.SearchInputDelay;
            }

            set
            {
                if (settings.Properties.SearchInputDelay != value)
                {
                    settings.Properties.SearchInputDelay = value;
                    UpdateSettings();
                }
            }
        }

        public bool SearchQueryTuningEnabled
        {
            get
            {
                return settings.Properties.SearchQueryTuningEnabled;
            }

            set
            {
                if (settings.Properties.SearchQueryTuningEnabled != value)
                {
                    settings.Properties.SearchQueryTuningEnabled = value;
                    UpdateSettings();
                }
            }
        }

        public bool SearchWaitForSlowResults
        {
            get
            {
                return settings.Properties.SearchWaitForSlowResults;
            }

            set
            {
                if (settings.Properties.SearchWaitForSlowResults != value)
                {
                    settings.Properties.SearchWaitForSlowResults = value;
                    UpdateSettings();
                }
            }
        }

        public int SearchClickedItemWeight
        {
            get
            {
                return settings.Properties.SearchClickedItemWeight;
            }

            set
            {
                if (settings.Properties.SearchClickedItemWeight != value)
                {
                    settings.Properties.SearchClickedItemWeight = value;
                    UpdateSettings();
                }
            }
        }

        public HotkeySettings OpenFileLocation
        {
            get
            {
                return settings.Properties.OpenFileLocation;
            }

            set
            {
                if (settings.Properties.OpenFileLocation != value)
                {
                    settings.Properties.OpenFileLocation = value ?? settings.Properties.DefaultOpenFileLocation;
                    UpdateSettings();
                }
            }
        }

        public HotkeySettings CopyPathLocation
        {
            get
            {
                return settings.Properties.CopyPathLocation;
            }

            set
            {
                if (settings.Properties.CopyPathLocation != value)
                {
                    settings.Properties.CopyPathLocation = value ?? settings.Properties.DefaultCopyPathLocation;
                    UpdateSettings();
                }
            }
        }

        public bool OverrideWinRKey
        {
            get
            {
                return settings.Properties.OverrideWinkeyR;
            }

            set
            {
                if (settings.Properties.OverrideWinkeyR != value)
                {
                    settings.Properties.OverrideWinkeyR = value;
                    UpdateSettings();
                }
            }
        }

        public bool OverrideWinSKey
        {
            get
            {
                return settings.Properties.OverrideWinkeyS;
            }

            set
            {
                if (settings.Properties.OverrideWinkeyS != value)
                {
                    settings.Properties.OverrideWinkeyS = value;
                    UpdateSettings();
                }
            }
        }

        public bool IgnoreHotkeysInFullScreen
        {
            get
            {
                return settings.Properties.IgnoreHotkeysInFullscreen;
            }

            set
            {
                if (settings.Properties.IgnoreHotkeysInFullscreen != value)
                {
                    settings.Properties.IgnoreHotkeysInFullscreen = value;
                    UpdateSettings();
                }
            }
        }

        public bool ClearInputOnLaunch
        {
            get
            {
                return settings.Properties.ClearInputOnLaunch;
            }

            set
            {
                if (settings.Properties.ClearInputOnLaunch != value)
                {
                    settings.Properties.ClearInputOnLaunch = value;
                    UpdateSettings();
                }
            }
        }

        public bool TabSelectsContextButtons
        {
            get
            {
                return settings.Properties.TabSelectsContextButtons;
            }

            set
            {
                if (settings.Properties.TabSelectsContextButtons != value)
                {
                    settings.Properties.TabSelectsContextButtons = value;
                    UpdateSettings();
                }
            }
        }

        public bool GenerateThumbnailsFromFiles
        {
            get
            {
                return settings.Properties.GenerateThumbnailsFromFiles;
            }

            set
            {
                if (settings.Properties.GenerateThumbnailsFromFiles != value)
                {
                    settings.Properties.GenerateThumbnailsFromFiles = value;
                    UpdateSettings();
                }
            }
        }

        public bool UsePinyin
        {
            get
            {
                return settings.Properties.UsePinyin;
            }

            set
            {
                if (settings.Properties.UsePinyin != value)
                {
                    settings.Properties.UsePinyin = value;
                    UpdateSettings();
                }
            }
        }

        public int ShowPluginsOverviewIndex
        {
            get
            {
                return settings.Properties.ShowPluginsOverview;
            }

            set
            {
                if (settings.Properties.ShowPluginsOverview != value)
                {
                    settings.Properties.ShowPluginsOverview = value;
                    UpdateSettings();
                }
            }
        }

        public int TitleFontSize
        {
            get
            {
                return settings.Properties.TitleFontSize;
            }

            set
            {
                if (settings.Properties.TitleFontSize != value)
                {
                    settings.Properties.TitleFontSize = value;
                    UpdateSettings();
                }
            }
        }

        private ObservableCollection<PowerLauncherPluginViewModel> _plugins;

        public ObservableCollection<PowerLauncherPluginViewModel> Plugins
        {
            get
            {
                if (_plugins == null)
                {
                    _plugins = new ObservableCollection<PowerLauncherPluginViewModel>(settings.Plugins.Select(x => new PowerLauncherPluginViewModel(x, isDark)));
                    foreach (var plugin in Plugins)
                    {
                        plugin.PropertyChanged += OnPluginInfoChange;
                    }
                }

                return _plugins;
            }
        }

        public bool ShowPluginsAreGpoManagedInfo
        {
            get => EnablePowerLauncher && settings.Plugins.Any() && Plugins.Any(x => x.EnabledGpoRuleIsConfigured);
        }

        public bool ShowAllPluginsDisabledWarning
        {
            get => EnablePowerLauncher && settings.Plugins.Any() && Plugins.All(x => x.Disabled);
        }

        public bool ShowPluginsLoadingMessage
        {
            get => EnablePowerLauncher && !Plugins.Any();
        }

        public bool IsUpToDate(PowerLauncherSettings settings)
        {
            return this.settings.Equals(settings);
        }

        public void SearchPlugins()
        {
            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                var plugins = settings.Plugins.Where(p => p.Name.StartsWith(SearchText, StringComparison.OrdinalIgnoreCase) || p.Name.IndexOf($" {SearchText}", StringComparison.OrdinalIgnoreCase) > 0);
                _plugins = new ObservableCollection<PowerLauncherPluginViewModel>(plugins.Select(x => new PowerLauncherPluginViewModel(x, isDark)));
                foreach (var plugin in _plugins)
                {
                    plugin.PropertyChanged += OnPluginInfoChange;
                }
            }
            else
            {
                _plugins = null;
            }

            OnPropertyChanged(nameof(Plugins));
        }
    }
}
