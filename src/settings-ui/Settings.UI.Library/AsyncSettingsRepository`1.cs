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
    /// <summary>
    /// Async settings repository implementation with caching and thread-safe access.
    /// Provides non-blocking settings operations suitable for UI applications.
    /// </summary>
    /// <typeparam name="T">The settings type.</typeparam>
    public sealed class AsyncSettingsRepository<T> : IAsyncSettingsRepository<T>, IDisposable
        where T : class, ISettingsConfig, new()
    {
        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);
        private readonly ISettingsUtils _settingsUtils;
        private readonly string _moduleName;
        private readonly string _fileName;

        private T _cachedSettings;
        private FileSystemWatcher _watcher;
        private bool _isDisposed;

        /// <inheritdoc/>
        public event Action<T> SettingsChanged;

        /// <summary>
        /// Initializes a new instance of the <see cref="AsyncSettingsRepository{T}"/> class.
        /// </summary>
        /// <param name="settingsUtils">The settings utilities instance.</param>
        /// <param name="fileName">The settings file name.</param>
        public AsyncSettingsRepository(ISettingsUtils settingsUtils, string fileName = "settings.json")
        {
            _settingsUtils = settingsUtils ?? throw new ArgumentNullException(nameof(settingsUtils));
            _fileName = fileName;

            // Get module name from type
            var settingsItem = new T();
            _moduleName = settingsItem.GetModuleName();

            InitializeWatcher();
        }

        /// <inheritdoc/>
        public T SettingsConfig
        {
            get
            {
                if (_cachedSettings == null)
                {
                    _semaphore.Wait();
                    try
                    {
                        if (_cachedSettings == null)
                        {
                            _cachedSettings = LoadSettingsInternal();
                        }
                    }
                    finally
                    {
                        _semaphore.Release();
                    }
                }

                return _cachedSettings;
            }

            private set => _cachedSettings = value;
        }

        /// <inheritdoc/>
        public async ValueTask<T> GetSettingsAsync(bool forceRefresh = false, CancellationToken cancellationToken = default)
        {
            if (!forceRefresh && _cachedSettings != null)
            {
                return _cachedSettings;
            }

            await _semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (!forceRefresh && _cachedSettings != null)
                {
                    return _cachedSettings;
                }

                _cachedSettings = await Task.Run(() => LoadSettingsInternal(), cancellationToken).ConfigureAwait(false);
                return _cachedSettings;
            }
            finally
            {
                _semaphore.Release();
            }
        }

        /// <inheritdoc/>
        public async ValueTask SaveSettingsAsync(T settings, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(settings);

            await _semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                // Temporarily stop watching to avoid self-triggered events
                StopWatching();

                await Task.Run(
                    () => _settingsUtils.SaveSettings(settings.ToJsonString(), _moduleName, _fileName),
                    cancellationToken).ConfigureAwait(false);

                _cachedSettings = settings;
            }
            finally
            {
                StartWatching();
                _semaphore.Release();
            }
        }

        /// <inheritdoc/>
        public bool ReloadSettings()
        {
            _semaphore.Wait();
            try
            {
                var newSettings = LoadSettingsInternal();
                if (newSettings != null)
                {
                    _cachedSettings = newSettings;
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to reload settings for {_moduleName}", ex);
                return false;
            }
            finally
            {
                _semaphore.Release();
            }
        }

        /// <inheritdoc/>
        public async ValueTask<bool> ReloadSettingsAsync(CancellationToken cancellationToken = default)
        {
            await _semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var newSettings = await Task.Run(() => LoadSettingsInternal(), cancellationToken).ConfigureAwait(false);
                if (newSettings != null)
                {
                    _cachedSettings = newSettings;
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to reload settings for {_moduleName}", ex);
                return false;
            }
            finally
            {
                _semaphore.Release();
            }
        }

        /// <inheritdoc/>
        public void StopWatching()
        {
            if (_watcher != null)
            {
                _watcher.EnableRaisingEvents = false;
            }
        }

        /// <inheritdoc/>
        public void StartWatching()
        {
            if (_watcher != null)
            {
                _watcher.EnableRaisingEvents = true;
            }
        }

        private T LoadSettingsInternal()
        {
            try
            {
                return _settingsUtils.GetSettingsOrDefault<T>(_moduleName, _fileName);
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to load settings for {_moduleName}", ex);
                return new T();
            }
        }

        private void InitializeWatcher()
        {
            try
            {
                var filePath = _settingsUtils.GetSettingsFilePath(_moduleName, _fileName);
                var directory = Path.GetDirectoryName(filePath);
                var fileName = Path.GetFileName(filePath);

                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                _watcher = new FileSystemWatcher(directory, fileName);
                _watcher.NotifyFilter = NotifyFilters.LastWrite;
                _watcher.Changed += OnWatcherChanged;
                _watcher.EnableRaisingEvents = true;
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to initialize settings watcher for {typeof(T).Name}", ex);
            }
        }

        private async void OnWatcherChanged(object sender, FileSystemEventArgs e)
        {
            // Wait a bit for the file write to complete and retry if needed
            for (int i = 0; i < 5; i++)
            {
                await Task.Delay(100).ConfigureAwait(false);
                if (await ReloadSettingsAsync().ConfigureAwait(false))
                {
                    SettingsChanged?.Invoke(_cachedSettings);
                    return;
                }
            }
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            if (!_isDisposed)
            {
                _watcher?.Dispose();
                _semaphore?.Dispose();
                _isDisposed = true;
            }
        }
    }
}
