// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using KeystrokeOverlayUI.Models;
using Microsoft.UI;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace KeystrokeOverlayUI
{
    public class OverlaySettings : IDisposable
    {
        // Event to notify ViewModel when settings change
        public event Action<ModuleProperties> SettingsUpdated;

        private const string ModuleName = "Keystroke Overlay";
        private readonly string _settingsFilePath;
        private FileSystemWatcher _watcher;

        public OverlaySettings()
        {
            // Path: %LOCALAPPDATA%\Microsoft\PowerToys\Keystroke Overlay\settings.json
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            _settingsFilePath = Path.Combine(localAppData, "Microsoft", "PowerToys", ModuleName, "settings.json");
        }

        public void Initialize()
        {
            // 1. Load initial settings
            LoadSettings();

            // 2. Watch for changes
            SetupWatcher();
        }

        private void SetupWatcher()
        {
            try
            {
                string folder = Path.GetDirectoryName(_settingsFilePath);
                if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
                {
                    return;
                }

                _watcher = new FileSystemWatcher(folder, "settings.json")
                {
                    NotifyFilter = NotifyFilters.LastWrite,
                    EnableRaisingEvents = true,
                };

                // Debounce logic (FileWatcher often fires twice)
                DateTime lastRead = DateTime.MinValue;
                _watcher.Changed += (s, e) =>
                {
                    if ((DateTime.Now - lastRead).TotalMilliseconds < 500)
                    {
                        return;
                    }

                    lastRead = DateTime.Now;

                    // Give the writing process (dllmain) a moment to close the file
                    Task.Delay(100).ContinueWith(_ => LoadSettings());
                };
            }
            catch
            {
                // Watcher might fail if permissions are weird, just ignore
            }
        }

        private void LoadSettings()
        {
            if (!File.Exists(_settingsFilePath))
            {
                return;
            }

            try
            {
                // Retry loop in case file is locked by dllmain
                for (int i = 0; i < 3; i++)
                {
                    try
                    {
                        string json = File.ReadAllText(_settingsFilePath);
                        var root = JsonSerializer.Deserialize<ModuleSettingsRoot>(json);

                        if (root?.Properties != null)
                        {
                            SettingsUpdated?.Invoke(root.Properties);
                        }

                        break;
                    }
                    catch (IOException)
                    {
                        System.Threading.Thread.Sleep(50);
                    }
                }
            }
            catch
            {
                // Log error or ignore
            }
        }

        public void Dispose()
        {
            if (_watcher != null)
            {
                _watcher.Dispose();
                _watcher = null;
            }

            GC.SuppressFinalize(this);
        }
    }
}
