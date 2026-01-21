// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    // This Singleton class is a wrapper around the settings configurations that are accessed by viewmodels.
    // This class can have only one instance and therefore the settings configurations are common to all.
    public sealed class SettingsRepository<T> : ISettingsRepository<T>, IDisposable
        where T : class, ISettingsConfig, new()
    {
        private static readonly Lock _SettingsRepoLock = new Lock();

        private static SettingsUtils _settingsUtils;

        private static SettingsRepository<T> settingsRepository;

        private T settingsConfig;

        private FileSystemWatcher _watcher;

        public event Action<T> SettingsChanged;

        // Suppressing the warning as this is a singleton class and this method is
        // necessarily static
#pragma warning disable CA1000 // Do not declare static members on generic types
        public static SettingsRepository<T> GetInstance(SettingsUtils settingsUtils)
#pragma warning restore CA1000 // Do not declare static members on generic types
        {
            // To ensure that only one instance of Settings Repository is created in a multi-threaded environment.
            lock (_SettingsRepoLock)
            {
                if (settingsRepository == null)
                {
                    settingsRepository = new SettingsRepository<T>();
                    _settingsUtils = settingsUtils ?? throw new ArgumentNullException(nameof(settingsUtils));
                    settingsRepository.InitializeWatcher();
                }

                return settingsRepository;
            }
        }

        // The Singleton class must have a private constructor so that it cannot be instantiated by any other object other than itself.
        private SettingsRepository()
        {
        }

        private void InitializeWatcher()
        {
            try
            {
                var settingsItem = new T();
                var filePath = _settingsUtils.GetSettingsFilePath(settingsItem.GetModuleName());
                var directory = Path.GetDirectoryName(filePath);
                var fileName = Path.GetFileName(filePath);

                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                _watcher = new FileSystemWatcher(directory, fileName);
                _watcher.NotifyFilter = NotifyFilters.LastWrite;
                _watcher.Changed += Watcher_Changed;
                _watcher.EnableRaisingEvents = true;
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to initialize settings watcher for {typeof(T).Name}", ex);
            }
        }

        private async void Watcher_Changed(object sender, FileSystemEventArgs e)
        {
            // Wait a bit for the file write to complete and retry if needed
            for (int i = 0; i < 5; i++)
            {
                await Task.Delay(100).ConfigureAwait(false);
                if (ReloadSettings())
                {
                    SettingsChanged?.Invoke(SettingsConfig);
                    return;
                }
            }
        }

        public bool ReloadSettings()
        {
            try
            {
                T settingsItem = new T();
                settingsConfig = _settingsUtils.GetSettings<T>(settingsItem.GetModuleName());

                SettingsConfig = settingsConfig;

                return true;
            }
            catch
            {
                return false;
            }
        }

        // Settings configurations shared across all viewmodels
        public T SettingsConfig
        {
            get
            {
                if (settingsConfig == null)
                {
                    T settingsItem = new T();
                    settingsConfig = _settingsUtils.GetSettingsOrDefault<T>(settingsItem.GetModuleName());
                }

                return settingsConfig;
            }

            set
            {
                if (value != null)
                {
                    settingsConfig = value;
                }
            }
        }

        public void StopWatching()
        {
            if (_watcher != null)
            {
                _watcher.EnableRaisingEvents = false;
            }
        }

        public void StartWatching()
        {
            if (_watcher != null)
            {
                _watcher.EnableRaisingEvents = true;
            }
        }

        public void Dispose()
        {
            _watcher?.Dispose();
        }
    }
}
