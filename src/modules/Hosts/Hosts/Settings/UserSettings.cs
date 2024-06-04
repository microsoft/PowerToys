// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO.Abstractions;
using System.Threading;
using HostsUILib.Settings;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Utilities;

namespace Hosts.Settings
{
    public class UserSettings : IUserSettings
    {
        private const string HostsModuleName = "Hosts";
        private const int MaxNumberOfRetry = 5;

        private readonly SettingsUtils _settingsUtils;
        private readonly IFileSystemWatcher _watcher;
        private readonly object _loadingSettingsLock = new object();

        public bool ShowStartupWarning { get; private set; }

        private bool _loopbackDuplicates;

        public bool LoopbackDuplicates
        {
            get => _loopbackDuplicates;
            set
            {
                if (_loopbackDuplicates != value)
                {
                    _loopbackDuplicates = value;
                    LoopbackDuplicatesChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        // Moved from Settings.UI.Library
        public HostsAdditionalLinesPosition AdditionalLinesPosition { get; private set; }

        // Moved from Settings.UI.Library
        public HostsEncoding Encoding { get; set; }

        public event EventHandler LoopbackDuplicatesChanged;

        public UserSettings()
        {
            _settingsUtils = new SettingsUtils();
            ShowStartupWarning = true;
            LoopbackDuplicates = false;
            AdditionalLinesPosition = HostsAdditionalLinesPosition.Top;
            Encoding = HostsEncoding.Utf8;

            LoadSettingsFromJson();

            _watcher = Helper.GetFileWatcher(HostsModuleName, "settings.json", () => LoadSettingsFromJson());
        }

        private void LoadSettingsFromJson()
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

                        if (!_settingsUtils.SettingsExists(HostsModuleName))
                        {
                            // Logger needs to be abstracted
                            Logger.LogInfo("Hosts settings.json was missing, creating a new one");
                            var defaultSettings = new HostsSettings();
                            defaultSettings.Save(_settingsUtils);
                        }

                        var settings = _settingsUtils.GetSettingsOrDefault<HostsSettings>(HostsModuleName);
                        if (settings != null)
                        {
                            ShowStartupWarning = settings.Properties.ShowStartupWarning;
                            AdditionalLinesPosition = (HostsAdditionalLinesPosition)settings.Properties.AdditionalLinesPosition;
                            Encoding = (HostsEncoding)settings.Properties.Encoding;
                            LoopbackDuplicates = settings.Properties.LoopbackDuplicates;
                        }

                        retry = false;
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
    }
}
