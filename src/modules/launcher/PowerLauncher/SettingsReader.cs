// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Threading;
using System.Windows.Input;
using global::PowerToys.GPOWrapper;
using Microsoft.PowerToys.Settings.UI.Library;
using PowerLauncher.Helper;
using PowerLauncher.Plugin;
using Wox.Infrastructure.Hotkey;
using Wox.Infrastructure.UserSettings;
using Wox.Plugin;
using Wox.Plugin.Logger;
using JsonException = System.Text.Json.JsonException;

namespace PowerLauncher
{
    // Watch for /Local/Microsoft/PowerToys/Launcher/Settings.json changes
    public class SettingsReader : BaseModel
    {
        private readonly SettingsUtils _settingsUtils;

        private const int MaxRetries = 10;
        private static readonly object _readSyncObject = new object();
        private readonly PowerToysRunSettings _settings;
        private readonly ThemeManager _themeManager;
        private Action _refreshPluginsOverviewCallback;

        private IFileSystemWatcher _watcher;

        public SettingsReader(PowerToysRunSettings settings, ThemeManager themeManager)
        {
            _settingsUtils = new SettingsUtils();
            _settings = settings;
            _themeManager = themeManager;

            var overloadSettings = _settingsUtils.GetSettingsOrDefault<PowerLauncherSettings>(PowerLauncherSettings.ModuleName);
            UpdateSettings(overloadSettings);
            _settingsUtils.SaveSettings(overloadSettings.ToJsonString(), PowerLauncherSettings.ModuleName);
        }

        public void CreateSettingsIfNotExists()
        {
            if (!_settingsUtils.SettingsExists(PowerLauncherSettings.ModuleName))
            {
                Log.Info("PT Run settings.json was missing, creating a new one", GetType());

                var defaultSettings = new PowerLauncherSettings();
                defaultSettings.Plugins = GetDefaultPluginsSettings();
                defaultSettings.Save(_settingsUtils);
            }
        }

        public void ReadSettingsOnChange()
        {
            _watcher = Microsoft.PowerToys.Settings.UI.Library.Utilities.Helper.GetFileWatcher(
                PowerLauncherSettings.ModuleName,
                "settings.json",
                () =>
                {
                    Log.Info("Settings were changed. Read settings.", GetType());
                    ReadSettings();
                });
        }

        public void ReadSettings()
        {
            Monitor.Enter(_readSyncObject);
            var retry = true;
            var retryCount = 0;
            while (retry)
            {
                try
                {
                    retryCount++;
                    CreateSettingsIfNotExists();

                    var overloadSettings = _settingsUtils.GetSettingsOrDefault<PowerLauncherSettings>(PowerLauncherSettings.ModuleName);
                    if (overloadSettings != null)
                    {
                        Log.Info($"Successfully read new settings. retryCount={retryCount}", GetType());
                    }

                    foreach (var setting in overloadSettings.Plugins)
                    {
                        var plugin = PluginManager.AllPlugins.FirstOrDefault(x => x.Metadata.ID == setting.Id);
                        plugin?.Update(setting, App.API, _refreshPluginsOverviewCallback);
                    }

                    var openPowerlauncher = ConvertHotkey(overloadSettings.Properties.OpenPowerLauncher);
                    if (_settings.Hotkey != openPowerlauncher)
                    {
                        _settings.Hotkey = openPowerlauncher;
                    }

                    if (_settings.UseCentralizedKeyboardHook != overloadSettings.Properties.UseCentralizedKeyboardHook)
                    {
                        _settings.UseCentralizedKeyboardHook = overloadSettings.Properties.UseCentralizedKeyboardHook;
                    }

                    if (_settings.SearchQueryResultsWithDelay != overloadSettings.Properties.SearchQueryResultsWithDelay)
                    {
                        _settings.SearchQueryResultsWithDelay = overloadSettings.Properties.SearchQueryResultsWithDelay;
                    }

                    if (_settings.SearchInputDelay != overloadSettings.Properties.SearchInputDelay)
                    {
                        _settings.SearchInputDelay = overloadSettings.Properties.SearchInputDelay;
                    }

                    if (_settings.SearchInputDelayFast != overloadSettings.Properties.SearchInputDelayFast)
                    {
                        _settings.SearchInputDelayFast = overloadSettings.Properties.SearchInputDelayFast;
                    }

                    if (_settings.SearchClickedItemWeight != overloadSettings.Properties.SearchClickedItemWeight)
                    {
                        _settings.SearchClickedItemWeight = overloadSettings.Properties.SearchClickedItemWeight;
                    }

                    if (_settings.SearchQueryTuningEnabled != overloadSettings.Properties.SearchQueryTuningEnabled)
                    {
                        _settings.SearchQueryTuningEnabled = overloadSettings.Properties.SearchQueryTuningEnabled;
                    }

                    if (_settings.SearchWaitForSlowResults != overloadSettings.Properties.SearchWaitForSlowResults)
                    {
                        _settings.SearchWaitForSlowResults = overloadSettings.Properties.SearchWaitForSlowResults;
                    }

                    if (_settings.MaxResultsToShow != overloadSettings.Properties.MaximumNumberOfResults)
                    {
                        _settings.MaxResultsToShow = overloadSettings.Properties.MaximumNumberOfResults;
                    }

                    if (_settings.IgnoreHotkeysOnFullscreen != overloadSettings.Properties.IgnoreHotkeysInFullscreen)
                    {
                        _settings.IgnoreHotkeysOnFullscreen = overloadSettings.Properties.IgnoreHotkeysInFullscreen;
                    }

                    if (_settings.ClearInputOnLaunch != overloadSettings.Properties.ClearInputOnLaunch)
                    {
                        _settings.ClearInputOnLaunch = overloadSettings.Properties.ClearInputOnLaunch;
                    }

                    if (_settings.TabSelectsContextButtons != overloadSettings.Properties.TabSelectsContextButtons)
                    {
                        _settings.TabSelectsContextButtons = overloadSettings.Properties.TabSelectsContextButtons;
                    }

                    if (_settings.Theme != overloadSettings.Properties.Theme)
                    {
                        _settings.Theme = overloadSettings.Properties.Theme;
                        _themeManager.SetTheme(true);
                    }

                    if (_settings.StartupPosition != overloadSettings.Properties.Position)
                    {
                        _settings.StartupPosition = overloadSettings.Properties.Position;
                    }

                    if (_settings.GenerateThumbnailsFromFiles != overloadSettings.Properties.GenerateThumbnailsFromFiles)
                    {
                        _settings.GenerateThumbnailsFromFiles = overloadSettings.Properties.GenerateThumbnailsFromFiles;
                    }

                    if (_settings.ShouldUsePinyin != overloadSettings.Properties.UsePinyin)
                    {
                        _settings.ShouldUsePinyin = overloadSettings.Properties.UsePinyin;
                    }

                    if (_settings.ShowPluginsOverview != (PowerToysRunSettings.ShowPluginsOverviewMode)overloadSettings.Properties.ShowPluginsOverview)
                    {
                        _settings.ShowPluginsOverview = (PowerToysRunSettings.ShowPluginsOverviewMode)overloadSettings.Properties.ShowPluginsOverview;
                    }

                    if (_settings.TitleFontSize != overloadSettings.Properties.TitleFontSize)
                    {
                        _settings.TitleFontSize = overloadSettings.Properties.TitleFontSize;
                    }

                    retry = false;
                }

                // the settings application can hold a lock on the settings.json file which will result in a IOException.
                // This should be changed to properly synch with the settings app instead of retrying.
                catch (IOException e)
                {
                    if (retryCount > MaxRetries)
                    {
                        retry = false;
                        Log.Exception($"Failed to Deserialize PowerToys settings, Retrying {e.Message}", e, GetType());
                    }
                    else
                    {
                        Thread.Sleep(1000);
                    }
                }
                catch (JsonException e)
                {
                    if (retryCount > MaxRetries)
                    {
                        retry = false;
                        Log.Exception($"Failed to Deserialize PowerToys settings, Creating new settings as file could be corrupted {e.Message}", e, GetType());

                        // Settings.json could possibly be corrupted. To mitigate this we delete the
                        // current file and replace it with a correct json value.
                        _settingsUtils.DeleteSettings(PowerLauncherSettings.ModuleName);
                        CreateSettingsIfNotExists();
                        ErrorReporting.ShowMessageBox(Properties.Resources.deserialization_error_title, Properties.Resources.deserialization_error_message);
                    }
                    else
                    {
                        Thread.Sleep(1000);
                    }
                }
            }

            Monitor.Exit(_readSyncObject);
        }

        public void SetRefreshPluginsOverviewCallback(Action callback)
        {
            _refreshPluginsOverviewCallback = callback;
        }

        private static string ConvertHotkey(HotkeySettings hotkey)
        {
            Key key = KeyInterop.KeyFromVirtualKey(hotkey.Code);
            HotkeyModel model = new HotkeyModel(hotkey.Alt, hotkey.Shift, hotkey.Win, hotkey.Ctrl, key);
            return model.ToString();
        }

        private static string GetIcon(PluginMetadata metadata, string iconPath)
        {
            return Path.Combine(metadata.PluginDirectory, iconPath);
        }

        private static IEnumerable<PowerLauncherPluginSettings> GetDefaultPluginsSettings()
        {
            return PluginManager.AllPlugins.Select(x => new PowerLauncherPluginSettings()
            {
                Id = x.Metadata.ID,
                Name = x.Plugin == null ? x.Metadata.Name : x.Plugin.Name,
                Description = x.Plugin?.Description,
                Author = x.Metadata.Author,
                Disabled = x.Metadata.Disabled,
                IsGlobal = x.Metadata.IsGlobal,
                ActionKeyword = x.Metadata.ActionKeyword,
                IconPathDark = GetIcon(x.Metadata, x.Metadata.IcoPathDark),
                IconPathLight = GetIcon(x.Metadata, x.Metadata.IcoPathLight),
                AdditionalOptions = x.Plugin is ISettingProvider ? (x.Plugin as ISettingProvider).AdditionalOptions : new List<PluginAdditionalOption>(),
                EnabledPolicyUiState = (int)GpoRuleConfigured.NotConfigured,
            });
        }

        /// <summary>
        /// Add new plugins and updates properties and additional options for existing ones
        /// </summary>
        private static void UpdateSettings(PowerLauncherSettings settings)
        {
            var defaultPlugins = GetDefaultPluginsSettings().ToDictionary(x => x.Id);
            var defaultPluginsByName = GetDefaultPluginsSettings().ToDictionary(x => x.Name);

            foreach (PowerLauncherPluginSettings plugin in settings.Plugins)
            {
                PowerLauncherPluginSettings value = null;
                if ((plugin.Id != null && defaultPlugins.TryGetValue(plugin.Id, out value)) || (!string.IsNullOrEmpty(plugin.Name) && defaultPluginsByName.TryGetValue(plugin.Name, out value)))
                {
                    var id = value.Id;
                    var name = value.Name;
                    var additionalOptions = plugin.AdditionalOptions != null ? CombineAdditionalOptions(value.AdditionalOptions, plugin.AdditionalOptions) : value.AdditionalOptions;
                    var enabledPolicyState = GPOWrapper.GetRunPluginEnabledValue(id);
                    plugin.Name = name;
                    plugin.Description = value.Description;
                    plugin.Author = value.Author;
                    plugin.IconPathDark = value.IconPathDark;
                    plugin.IconPathLight = value.IconPathLight;
                    plugin.EnabledPolicyUiState = (int)enabledPolicyState;
                    defaultPlugins[id] = plugin;
                    defaultPlugins[id].AdditionalOptions = additionalOptions;
                }
            }

            settings.Plugins = defaultPlugins.Values.ToList();
        }

        private static Dictionary<string, PluginAdditionalOption>.ValueCollection CombineAdditionalOptions(IEnumerable<PluginAdditionalOption> defaultAdditionalOptions, IEnumerable<PluginAdditionalOption> additionalOptions)
        {
            var defaultOptions = defaultAdditionalOptions.ToDictionary(x => x.Key);
            foreach (var option in additionalOptions)
            {
                if (option.Key != null && defaultOptions.TryGetValue(option.Key, out PluginAdditionalOption defaultOption))
                {
                    defaultOption.Value = option.Value;
                    defaultOption.ComboBoxValue = option.ComboBoxValue;
                    defaultOption.TextValue = option.TextValue;
                    defaultOption.NumberValue = option.NumberValue;
                }
            }

            return defaultOptions.Values;
        }
    }
}
