// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.ComponentModel.Composition;
using System.IO;
using System.IO.Abstractions;
using System.Threading;

using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Utilities;

namespace PowerOCR.Settings
{
    [Export(typeof(IUserSettings))]
    public class UserSettings : IUserSettings
    {
        private readonly SettingsUtils _settingsUtils;
        private const string PowerOcrModuleName = "TextExtractor";
        private const string DefaultActivationShortcut = "Win + Shift + O";
        private const int MaxNumberOfRetry = 5;
        private const int SettingsReadOnChangeDelayInMs = 300;

        private readonly IFileSystemWatcher _watcher;
        private readonly Lock _loadingSettingsLock = new();

        // Simplified: default ON; backend handles actual availability.
        [ImportingConstructor]
        public UserSettings(Helpers.IThrottledActionInvoker throttledActionInvoker)
        {
            _settingsUtils = new SettingsUtils();
            ActivationShortcut = new SettingItem<string>(DefaultActivationShortcut);
            PreferredLanguage = new SettingItem<string>(string.Empty);
            UseAITextRecognition = new SettingItem<bool>(true); // default ON: backend will fallback silently if unusable

            LoadSettingsFromJson();

            // delay loading settings on change by some time to avoid file in use exception
            _watcher = Helper.GetFileWatcher(PowerOcrModuleName, "settings.json", () => throttledActionInvoker.ScheduleAction(LoadSettingsFromJson, SettingsReadOnChangeDelayInMs));
        }

        public SettingItem<string> ActivationShortcut { get; private set; }

        public SettingItem<string> PreferredLanguage { get; private set; }

        // New setting to control AI recognizer usage (mirrors PowerOcrProperties.UseLocalAIIfAvailable)
        public SettingItem<bool> UseAITextRecognition { get; private set; }

        private void LoadSettingsFromJson()
        {
            // TODO this IO call should by Async, update GetFileWatcher helper to support async
            lock (_loadingSettingsLock)
            {
                var retry = true;
                var retryCount = 0;

                while (retry)
                {
                    try
                    {
                        retryCount++;

                        if (!_settingsUtils.SettingsExists(PowerOcrModuleName))
                        {
                            Logger.LogInfo("TextExtractor settings.json was missing, creating a new one");
                            var defaultPowerOcrSettings = new PowerOcrSettings();
                            defaultPowerOcrSettings.Save(_settingsUtils);
                        }

                        var settings = _settingsUtils.GetSettingsOrDefault<PowerOcrSettings>(PowerOcrModuleName);
                        if (settings != null)
                        {
                            ActivationShortcut.Value = settings.Properties.ActivationShortcut.ToString();
                            PreferredLanguage.Value = settings.Properties.PreferredLanguage.ToString();
                            UseAITextRecognition.Value = settings.Properties.UseLocalAIIfAvailable;
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
                    catch (Exception ex)
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

        public void SendSettingsTelemetry()
        {
            Logger.LogInfo("Sending settings telemetry");
            var settings = _settingsUtils.GetSettingsOrDefault<PowerOcrSettings>(PowerOcrModuleName);
            var properties = settings?.Properties;
            if (properties == null)
            {
                Logger.LogError("Failed to send settings telemetry");
                return;
            }

            // TODO: Send Telemetry when settings change
            // var telemetrySettings = new Telemetry.PowerOcrSettings(properties.VisibleColorFormats)
            // {
            //     ActivationShortcut = properties.ActivationShortcut.ToString(),
            //     ActivationBehavior = properties.ActivationAction.ToString(),
            //     ColorFormatForClipboard = properties.CopiedColorRepresentation.ToString(),
            //     ShowColorName = properties.ShowColorName,
            // };
            //
            // PowerToysTelemetry.Log.WriteEvent(telemetrySettings);
        }

        // Capability probing removed; backend handles availability lazily.
    }
}
