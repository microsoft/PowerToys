// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.ObjectModel;
using System.ComponentModel.Composition;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Threading;
using ColorPicker.Common;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Enumerations;
using Microsoft.PowerToys.Settings.UI.Library.Utilities;
using Microsoft.PowerToys.Telemetry;

namespace ColorPicker.Settings
{
    [Export(typeof(IUserSettings))]
    public class UserSettings : IUserSettings
    {
        private readonly ISettingsUtils _settingsUtils;
        private const string ColorPickerModuleName = "ColorPicker";
        private const string DefaultActivationShortcut = "Ctrl + Break";
        private const int MaxNumberOfRetry = 5;
        private const int SettingsReadOnChangeDelayInMs = 300;

        [System.Diagnostics.CodeAnalysis.SuppressMessage("CodeQuality", "IDE0052:Remove unread private members", Justification = "Actually, call back is LoadSettingsFromJson")]
        private readonly IFileSystemWatcher _watcher;

        private readonly object _loadingSettingsLock = new object();

        private bool _loadingColorsHistory;

        [ImportingConstructor]
        public UserSettings(Helpers.IThrottledActionInvoker throttledActionInvoker)
        {
            _settingsUtils = new SettingsUtils();
            ChangeCursor = new SettingItem<bool>(true);
            ActivationShortcut = new SettingItem<string>(DefaultActivationShortcut);
            CopiedColorRepresentation = new SettingItem<ColorRepresentationType>(ColorRepresentationType.HEX);
            ActivationAction = new SettingItem<ColorPickerActivationAction>(ColorPickerActivationAction.OpenEditor);
            ColorHistoryLimit = new SettingItem<int>(20);
            ColorHistory.CollectionChanged += ColorHistory_CollectionChanged;
            ShowColorName = new SettingItem<bool>(false);

            LoadSettingsFromJson();

            // delay loading settings on change by some time to avoid file in use exception
            _watcher = Helper.GetFileWatcher(ColorPickerModuleName, "settings.json", () => throttledActionInvoker.ScheduleAction(LoadSettingsFromJson, SettingsReadOnChangeDelayInMs));
        }

        private void ColorHistory_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (!_loadingColorsHistory)
            {
                var settings = _settingsUtils.GetSettingsOrDefault<ColorPickerSettings>(ColorPickerModuleName);
                settings.Properties.ColorHistory = ColorHistory.ToList();
                settings.Save(_settingsUtils);
            }
        }

        public SettingItem<string> ActivationShortcut { get; private set; }

        public SettingItem<bool> ChangeCursor { get; private set; }

        public SettingItem<ColorRepresentationType> CopiedColorRepresentation { get; set; }

        public SettingItem<ColorPickerActivationAction> ActivationAction { get; private set; }

        public RangeObservableCollection<string> ColorHistory { get; private set; } = new RangeObservableCollection<string>();

        public SettingItem<int> ColorHistoryLimit { get; }

        public ObservableCollection<string> VisibleColorFormats { get; private set; } = new ObservableCollection<string>();

        public SettingItem<bool> ShowColorName { get; }

        private void LoadSettingsFromJson()
        {
            // TODO this IO call should by Async, update GetFileWatcher helper to support async
            lock (_loadingSettingsLock)
            {
                {
                    var retry = true;
                    var retryCount = 0;

                    while (retry)
                    {
                        try
                        {
                            retryCount++;

                            if (!_settingsUtils.SettingsExists(ColorPickerModuleName))
                            {
                                Logger.LogInfo("ColorPicker settings.json was missing, creating a new one");
                                var defaultColorPickerSettings = new ColorPickerSettings();
                                defaultColorPickerSettings.Save(_settingsUtils);
                            }

                            var settings = _settingsUtils.GetSettingsOrDefault<ColorPickerSettings>(ColorPickerModuleName);
                            if (settings != null)
                            {
                                ChangeCursor.Value = settings.Properties.ChangeCursor;
                                ActivationShortcut.Value = settings.Properties.ActivationShortcut.ToString();
                                CopiedColorRepresentation.Value = settings.Properties.CopiedColorRepresentation;
                                ActivationAction.Value = settings.Properties.ActivationAction;
                                ColorHistoryLimit.Value = settings.Properties.ColorHistoryLimit;
                                ShowColorName.Value = settings.Properties.ShowColorName;

                                if (settings.Properties.ColorHistory == null)
                                {
                                    settings.Properties.ColorHistory = new System.Collections.Generic.List<string>();
                                }

                                _loadingColorsHistory = true;
                                ColorHistory.Clear();
                                foreach (var item in settings.Properties.ColorHistory)
                                {
                                    ColorHistory.Add(item);
                                }

                                _loadingColorsHistory = false;

                                VisibleColorFormats.Clear();
                                foreach (var item in settings.Properties.VisibleColorFormats)
                                {
                                    if (item.Value)
                                    {
                                        VisibleColorFormats.Add(item.Key);
                                    }
                                }
                            }

                            retry = false;
                        }
                        catch (IOException ex)
                        {
                            if (retryCount > MaxNumberOfRetry)
                            {
                                retry = false;
                            }

                            Logger.LogError("Failed to read changed settings", ex);
                            Thread.Sleep(500);
                        }
#pragma warning disable CA1031 // Do not catch general exception types
                        catch (Exception ex)
#pragma warning restore CA1031 // Do not catch general exception types
                        {
                            if (retryCount > MaxNumberOfRetry)
                            {
                                retry = false;
                            }

                            Logger.LogError("Failed to read changed settings", ex);
                            Thread.Sleep(500);
                        }
                    }
                }
            }
        }

        public void SendSettingsTelemetry()
        {
            Logger.LogInfo("Sending settings telemetry");
            var settings = _settingsUtils.GetSettingsOrDefault<ColorPickerSettings>(ColorPickerModuleName);
            var properties = settings?.Properties;
            if (properties == null)
            {
                Logger.LogError("Failed to send settings telemetry");
                return;
            }

            var telemetrySettings = new Telemetry.ColorPickerSettings(properties.VisibleColorFormats)
            {
                ActivationShortcut = properties.ActivationShortcut.ToString(),
                ActivationBehaviour = properties.ActivationAction.ToString(),
                ColorFormatForClipboard = properties.CopiedColorRepresentation.ToString(),
                ShowColorName = properties.ShowColorName,
            };

            PowerToysTelemetry.Log.WriteEvent(telemetrySettings);
        }
    }
}
