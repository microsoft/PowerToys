// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;

namespace Microsoft.PowerToys.Settings.UI.Services
{
    /// <summary>
    /// Implementation of ISettingsService that provides centralized settings management
    /// with async loading and caching support.
    /// </summary>
    public class SettingsService : ISettingsService
    {
        private readonly SettingsUtils _settingsUtils;
        private ISettingsRepository<GeneralSettings> _generalSettingsRepository;
        private bool _isLoaded;

        /// <summary>
        /// Initializes a new instance of the <see cref="SettingsService"/> class.
        /// </summary>
        public SettingsService()
            : this(SettingsUtils.Default)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SettingsService"/> class.
        /// </summary>
        /// <param name="settingsUtils">The settings utilities instance.</param>
        public SettingsService(SettingsUtils settingsUtils)
        {
            _settingsUtils = settingsUtils ?? throw new ArgumentNullException(nameof(settingsUtils));
        }

        /// <inheritdoc/>
        public ISettingsRepository<GeneralSettings> GeneralSettingsRepository
        {
            get
            {
                EnsureLoaded();
                return _generalSettingsRepository;
            }
        }

        /// <inheritdoc/>
        public GeneralSettings GeneralSettings
        {
            get
            {
                EnsureLoaded();
                return _generalSettingsRepository?.SettingsConfig;
            }
        }

        /// <inheritdoc/>
        public SettingsUtils SettingsUtils => _settingsUtils;

        /// <inheritdoc/>
        public bool IsLoaded => _isLoaded;

        /// <inheritdoc/>
        public event Action<GeneralSettings> GeneralSettingsChanged;

        /// <inheritdoc/>
        public Task LoadAsync(CancellationToken cancellationToken = default)
        {
            return Task.Run(
                () =>
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    // Load general settings repository
                    _generalSettingsRepository = SettingsRepository<GeneralSettings>.GetInstance(_settingsUtils);
                    _generalSettingsRepository.SettingsChanged += OnGeneralSettingsChanged;

                    _isLoaded = true;
                },
                cancellationToken);
        }

        /// <inheritdoc/>
        public ISettingsRepository<T> GetRepository<T>()
            where T : class, ISettingsConfig, new()
        {
            return SettingsRepository<T>.GetInstance(_settingsUtils);
        }

        /// <inheritdoc/>
        public Task SaveAsync<T>(T settings, CancellationToken cancellationToken = default)
            where T : class, ISettingsConfig, new()
        {
            return Task.Run(
                () =>
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var json = settings.ToJsonString();
                    _settingsUtils.SaveSettings(json, settings.GetModuleName());
                },
                cancellationToken);
        }

        private void EnsureLoaded()
        {
            if (!_isLoaded)
            {
                // Synchronously load if not already loaded
                _generalSettingsRepository = SettingsRepository<GeneralSettings>.GetInstance(_settingsUtils);
                _generalSettingsRepository.SettingsChanged += OnGeneralSettingsChanged;
                _isLoaded = true;
            }
        }

        private void OnGeneralSettingsChanged(GeneralSettings newSettings)
        {
            GeneralSettingsChanged?.Invoke(newSettings);
        }
    }
}
