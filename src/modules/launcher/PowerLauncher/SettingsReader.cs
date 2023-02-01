// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Threading;
using System.Windows.Input;
using Common.UI;
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
        private readonly ISettingsUtils _settingsUtils;

        private const int MaxRetries = 10;
        private static readonly object _readSyncObject = new object();
        private readonly PowerToysRunSettings _settings;
        private readonly ThemeManager _themeManager;

        private IFileSystemWatcher _watcher;

        public SettingsReader(PowerToysRunSettings settings, ThemeManager themeManager)
        {
            _settingsUtils = new SettingsUtils();
            _settings = settings;
            _themeManager = themeManager;

            var overloadSettings = _settingsUtils.GetSettingsOrDefault<PowerLauncherSettings>(PowerLauncherSettings.ModuleName);
            UpdateSettings(overloadSettings);
            _settingsUtils.SaveSettings(overloadSettings.ToJsonString(), PowerLauncherSettings.ModuleName);

            // Apply theme at startup
            _themeManager.ChangeTheme(_settings.Theme, true);
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
                        plugin?.Update(setting, App.API);
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
                        _themeManager.ChangeTheme(_settings.Theme, true);
                    }

                    if (_settings.StartupPosition != overloadSettings.Properties.Position)
                    {
                        _settings.StartupPosition = overloadSettings.Properties.Position;
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

        private static string ConvertHotkey(HotkeySettings hotkey)
        {
            Key key = KeyInterop.KeyFromVirtualKey(hotkey.Code);
            HotkeyModel model = new HotkeyModel(hotkey.Alt, hotkey.Shift, hotkey.Win, hotkey.Ctrl, key);
            return model.ToString();
        }

        private static string GetIcon(PluginMetadata metadata, string iconPath)
        {
            var pluginDirectory = Path.GetFileName(metadata.PluginDirectory);
            return Path.Combine(pluginDirectory, iconPath);
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
            });
        }

        /// <summary>
        /// Add new plugins and updates properties and additional options for existing ones
        /// </summary>
        private static void UpdateSettings(PowerLauncherSettings settings)
        {
            var defaultPlugins = GetDefaultPluginsSettings().ToDictionary(x => x.Id);
            foreach (PowerLauncherPluginSettings plugin in settings.Plugins)
            {
                if (defaultPlugins.ContainsKey(plugin.Id))
                {
                    var additionalOptions = CombineAdditionalOptions(defaultPlugins[plugin.Id].AdditionalOptions, plugin.AdditionalOptions);
                    plugin.Name = defaultPlugins[plugin.Id].Name;
                    plugin.Description = defaultPlugins[plugin.Id].Description;
                    plugin.Author = defaultPlugins[plugin.Id].Author;
                    plugin.IconPathDark = defaultPlugins[plugin.Id].IconPathDark;
                    plugin.IconPathLight = defaultPlugins[plugin.Id].IconPathLight;
                    defaultPlugins[plugin.Id] = plugin;
                    defaultPlugins[plugin.Id].AdditionalOptions = additionalOptions;
                }
            }

            settings.Plugins = defaultPlugins.Values.ToList();
        }

        private static IEnumerable<PluginAdditionalOption> CombineAdditionalOptions(IEnumerable<PluginAdditionalOption> defaultAdditionalOptions, IEnumerable<PluginAdditionalOption> additionalOptions)
        {
            var defaultOptions = defaultAdditionalOptions.ToDictionary(x => x.Key);
            foreach (var option in additionalOptions)
            {
                if (option.Key != null && defaultOptions.ContainsKey(option.Key))
                {
                    defaultOptions[option.Key].Value = option.Value;
                }
            }

            return defaultOptions.Values;
        }
    }
}
