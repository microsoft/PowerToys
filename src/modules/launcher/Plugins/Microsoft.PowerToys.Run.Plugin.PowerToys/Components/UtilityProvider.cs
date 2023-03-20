// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.PowerToys.Run.Plugin.PowerToys.Components;
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
                _utilities.Add(new Utility(UtilityKey.ColorPicker, "Color Picker", generalSettings.Enabled.ColorPicker));
            }

            if (GPOWrapper.GetConfiguredFancyZonesEnabledValue() != GpoRuleConfigured.Disabled)
            {
                _utilities.Add(new Utility(UtilityKey.FancyZones, "FancyZones", generalSettings.Enabled.FancyZones));
            }

            if (GPOWrapper.GetConfiguredHostsFileEditorEnabledValue() != GpoRuleConfigured.Disabled)
            {
                _utilities.Add(new Utility(UtilityKey.Hosts, "Hosts File Editor", generalSettings.Enabled.Hosts));
            }

            if (GPOWrapper.GetConfiguredScreenRulerEnabledValue() != GpoRuleConfigured.Disabled)
            {
                _utilities.Add(new Utility(UtilityKey.MeasureTool, "Screen Ruler", generalSettings.Enabled.MeasureTool));
            }

            if (GPOWrapper.GetConfiguredTextExtractorEnabledValue() != GpoRuleConfigured.Disabled)
            {
                _utilities.Add(new Utility(UtilityKey.PowerOCR, "Text Extractor", generalSettings.Enabled.PowerOCR));
            }

            if (GPOWrapper.GetConfiguredShortcutGuideEnabledValue() != GpoRuleConfigured.Disabled)
            {
                _utilities.Add(new Utility(UtilityKey.ShortcutGuide, "Shortcut Guide", generalSettings.Enabled.ShortcutGuide));
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
                                case UtilityKey.ColorPicker: u.Enabled = generalSettings.Enabled.ColorPicker; break;
                                case UtilityKey.FancyZones: u.Enabled = generalSettings.Enabled.FancyZones; break;
                                case UtilityKey.Hosts: u.Enabled = generalSettings.Enabled.Hosts; break;
                                case UtilityKey.PowerOCR: u.Enabled = generalSettings.Enabled.PowerOCR; break;
                                case UtilityKey.MeasureTool: u.Enabled = generalSettings.Enabled.MeasureTool; break;
                                case UtilityKey.ShortcutGuide: u.Enabled = generalSettings.Enabled.ShortcutGuide; break;
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
