// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using interop;
using Microsoft.PowerToys.Run.Plugin.PowerToys.Components;
using Microsoft.PowerToys.Run.Plugin.PowerToys.Properties;
using Microsoft.PowerToys.Settings.UI.Library;
using PowerToys.GPOWrapper;
using Wox.Plugin.Logger;

namespace Microsoft.PowerToys.Run.Plugin.PowerToys
{
    public class UtilityProvider : IDisposable
    {
        private const int MaxNumberOfRetry = 5;
        private readonly List<Utility> _utilities;
        private readonly FileSystemWatcher _watcher;
        private readonly object _loadingSettingsLock = new();
        private bool _disposed;

        public UtilityProvider()
        {
            var settingsUtils = new SettingsUtils();
            var generalSettings = settingsUtils.GetSettings<GeneralSettings>();

            _utilities = new List<Utility>();

            if (GPOWrapper.GetConfiguredColorPickerEnabledValue() != GpoRuleConfigured.Disabled)
            {
                _utilities.Add(new Utility(
                    UtilityKey.ColorPicker,
                    Resources.Color_Picker,
                    generalSettings.Enabled.ColorPicker,
                    (_) =>
                    {
                        using var eventHandle = new EventWaitHandle(false, EventResetMode.AutoReset, Constants.ShowColorPickerSharedEvent());
                        eventHandle.Set();
                        return true;
                    }));
            }

            if (GPOWrapper.GetConfiguredFancyZonesEnabledValue() != GpoRuleConfigured.Disabled)
            {
                _utilities.Add(new Utility(
                    UtilityKey.FancyZones,
                    Resources.FancyZones_Editor,
                    generalSettings.Enabled.FancyZones,
                    (_) =>
                    {
                        using var eventHandle = new EventWaitHandle(false, EventResetMode.AutoReset, Constants.FZEToggleEvent());
                        eventHandle.Set();
                        return true;
                    }));
            }

            if (GPOWrapper.GetConfiguredHostsFileEditorEnabledValue() != GpoRuleConfigured.Disabled)
            {
                _utilities.Add(new Utility(
                    UtilityKey.Hosts,
                    Resources.Hosts_File_Editor,
                    generalSettings.Enabled.Hosts,
                    (_) =>
                    {
                        using var eventHandle = new EventWaitHandle(false, EventResetMode.AutoReset, Constants.ShowHostsSharedEvent());
                        eventHandle.Set();
                        return true;
                    }));
            }

            if (GPOWrapper.GetConfiguredScreenRulerEnabledValue() != GpoRuleConfigured.Disabled)
            {
                _utilities.Add(new Utility(
                    UtilityKey.MeasureTool,
                    Resources.Screen_Ruler,
                    generalSettings.Enabled.MeasureTool,
                    (_) =>
                    {
                        using var eventHandle = new EventWaitHandle(false, EventResetMode.AutoReset, Constants.MeasureToolTriggerEvent());
                        eventHandle.Set();
                        return true;
                    }));
            }

            if (GPOWrapper.GetConfiguredTextExtractorEnabledValue() != GpoRuleConfigured.Disabled)
            {
                _utilities.Add(new Utility(
                    UtilityKey.PowerOCR,
                    Resources.Text_Extractor,
                    generalSettings.Enabled.PowerOcr,
                    (_) =>
                    {
                        using var eventHandle = new EventWaitHandle(false, EventResetMode.AutoReset, Constants.ShowPowerOCRSharedEvent());
                        eventHandle.Set();
                        return true;
                    }));
            }

            if (GPOWrapper.GetConfiguredShortcutGuideEnabledValue() != GpoRuleConfigured.Disabled)
            {
                _utilities.Add(new Utility(
                    UtilityKey.ShortcutGuide,
                    Resources.Shortcut_Guide,
                    generalSettings.Enabled.ShortcutGuide,
                    (_) =>
                    {
                        using var eventHandle = new EventWaitHandle(false, EventResetMode.AutoReset, Constants.ShortcutGuideTriggerEvent());
                        eventHandle.Set();
                        return true;
                    }));
            }

            if (GPOWrapper.GetConfiguredRegistryPreviewEnabledValue() != GpoRuleConfigured.Disabled)
            {
                _utilities.Add(new Utility(
                    UtilityKey.RegistryPreview,
                    Resources.Registry_Preview,
                    generalSettings.Enabled.RegistryPreview,
                    (_) =>
                    {
                        using var eventHandle = new EventWaitHandle(false, EventResetMode.AutoReset, Constants.RegistryPreviewTriggerEvent());
                        eventHandle.Set();
                        return true;
                    }));
            }

            if (GPOWrapper.GetConfiguredCropAndLockEnabledValue() != GpoRuleConfigured.Disabled)
            {
                _utilities.Add(new Utility(
                    UtilityKey.CropAndLock,
                    Resources.Crop_And_Lock_Thumbnail,
                    generalSettings.Enabled.CropAndLock,
                    (_) =>
                    {
                        // Wait for the Launcher window to be hidden and activate Crop And Lock in the correct window
                        var timer = new System.Timers.Timer(TimeSpan.FromMilliseconds(500));
                        timer.Elapsed += (_, _) =>
                        {
                            timer.Stop();
                            using var eventHandle = new EventWaitHandle(false, EventResetMode.AutoReset, Constants.CropAndLockThumbnailEvent());
                            eventHandle.Set();
                        };

                        timer.Start();
                        return true;
                    }));

                _utilities.Add(new Utility(
                    UtilityKey.CropAndLock,
                    Resources.Crop_And_Lock_Reparent,
                    generalSettings.Enabled.CropAndLock,
                    (_) =>
                    {
                        // Wait for the Launcher window to be hidden and activate Crop And Lock in the correct window
                        var timer = new System.Timers.Timer(TimeSpan.FromMilliseconds(500));
                        timer.Elapsed += (_, _) =>
                        {
                            timer.Stop();
                            using var eventHandle = new EventWaitHandle(false, EventResetMode.AutoReset, Constants.CropAndLockReparentEvent());
                            eventHandle.Set();
                        };

                        timer.Start();
                        return true;
                    }));
            }

            if (GPOWrapper.GetConfiguredEnvironmentVariablesEnabledValue() != GpoRuleConfigured.Disabled)
            {
                _utilities.Add(new Utility(
                    UtilityKey.EnvironmentVariables,
                    Resources.Environment_Variables,
                    generalSettings.Enabled.EnvironmentVariables,
                    (_) =>
                    {
                        using var eventHandle = new EventWaitHandle(false, EventResetMode.AutoReset, Constants.ShowEnvironmentVariablesSharedEvent());
                        eventHandle.Set();
                        return true;
                    }));
            }

            _watcher = new FileSystemWatcher
            {
                Path = Path.GetDirectoryName(settingsUtils.GetSettingsFilePath()),
                Filter = Path.GetFileName(settingsUtils.GetSettingsFilePath()),
                NotifyFilter = NotifyFilters.LastWrite,
            };

            _watcher.Changed += (s, e) => ReloadUtilities();
            _watcher.EnableRaisingEvents = true;
        }

        public IEnumerable<Utility> GetEnabledUtilities()
        {
            return _utilities.Where(u => u.Enabled);
        }

        private void ReloadUtilities()
        {
            lock (_loadingSettingsLock)
            {
                var retry = true;
                var retryCount = 0;

                while (retry)
                {
                    try
                    {
                        retryCount++;

                        var settingsUtils = new SettingsUtils();
                        var generalSettings = settingsUtils.GetSettings<GeneralSettings>();

                        foreach (var u in _utilities)
                        {
                            switch (u.Key)
                            {
                                case UtilityKey.ColorPicker: u.Enable(generalSettings.Enabled.ColorPicker); break;
                                case UtilityKey.FancyZones: u.Enable(generalSettings.Enabled.FancyZones); break;
                                case UtilityKey.Hosts: u.Enable(generalSettings.Enabled.Hosts); break;
                                case UtilityKey.PowerOCR: u.Enable(generalSettings.Enabled.PowerOcr); break;
                                case UtilityKey.MeasureTool: u.Enable(generalSettings.Enabled.MeasureTool); break;
                                case UtilityKey.ShortcutGuide: u.Enable(generalSettings.Enabled.ShortcutGuide); break;
                                case UtilityKey.RegistryPreview: u.Enable(generalSettings.Enabled.RegistryPreview); break;
                                case UtilityKey.CropAndLock: u.Enable(generalSettings.Enabled.CropAndLock); break;
                                case UtilityKey.EnvironmentVariables: u.Enable(generalSettings.Enabled.EnvironmentVariables); break;
                            }
                        }

                        retry = false;
                    }
                    catch (Exception ex)
                    {
                        if (retryCount > MaxNumberOfRetry)
                        {
                            Log.Exception("Failed to read changed settings", ex, typeof(UtilityProvider));
                            retry = false;
                        }

                        Thread.Sleep(500);
                    }
                }
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _watcher?.Dispose();
                    _disposed = true;
                }
            }
        }
    }
}
