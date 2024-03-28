// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.IO.Abstractions;
using System.Threading;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Utilities;

namespace Peek.UI
{
    public class UserSettings : IUserSettings
    {
        private const string PeekModuleName = "Peek";
        private const int MaxNumberOfRetry = 5;

        private readonly SettingsUtils _settingsUtils;
        private readonly IFileSystemWatcher _watcher;
        private readonly object _loadingSettingsLock = new();

        public bool CloseAfterLosingFocus { get; private set; }

        public UserSettings()
        {
            _settingsUtils = new SettingsUtils();
            CloseAfterLosingFocus = false;

            LoadSettingsFromJson();

            _watcher = Helper.GetFileWatcher(PeekModuleName, "settings.json", () => LoadSettingsFromJson());
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

                        if (!_settingsUtils.SettingsExists(PeekModuleName))
                        {
                            Logger.LogInfo("Peek settings.json was missing, creating a new one");
                            var defaultSettings = new PeekSettings();
                            defaultSettings.Save(_settingsUtils);
                        }

                        var settings = _settingsUtils.GetSettingsOrDefault<PeekSettings>(PeekModuleName);
                        if (settings != null)
                        {
                            CloseAfterLosingFocus = settings.Properties.CloseAfterLosingFocus.Value;
                        }

                        retry = false;
                    }
                    catch (IOException e)
                    {
                        if (retryCount > MaxNumberOfRetry)
                        {
                            retry = false;
                            Logger.LogError($"Failed to Deserialize PowerToys settings, Retrying {e.Message}", e);
                        }
                        else
                        {
                            Thread.Sleep(500);
                        }
                    }
                    catch (Exception ex)
                    {
                        retry = false;
                        Logger.LogError("Failed to read changed settings", ex);
                    }
                }
            }
        }
    }
}
