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
    /// Interface for settings management with async loading and caching support.
    /// </summary>
    public interface ISettingsService
    {
        /// <summary>
        /// Gets the general settings repository.
        /// </summary>
        ISettingsRepository<GeneralSettings> GeneralSettingsRepository { get; }

        /// <summary>
        /// Gets the general settings configuration.
        /// </summary>
        GeneralSettings GeneralSettings { get; }

        /// <summary>
        /// Gets the settings utilities instance.
        /// </summary>
        SettingsUtils SettingsUtils { get; }

        /// <summary>
        /// Gets a value indicating whether settings have been loaded.
        /// </summary>
        bool IsLoaded { get; }

        /// <summary>
        /// Loads settings asynchronously.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the async operation.</returns>
        Task LoadAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets a settings repository for the specified settings type.
        /// </summary>
        /// <typeparam name="T">The type of settings.</typeparam>
        /// <returns>The settings repository.</returns>
        ISettingsRepository<T> GetRepository<T>()
            where T : class, ISettingsConfig, new();

        /// <summary>
        /// Saves settings asynchronously.
        /// </summary>
        /// <typeparam name="T">The type of settings.</typeparam>
        /// <param name="settings">The settings to save.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the async operation.</returns>
        Task SaveAsync<T>(T settings, CancellationToken cancellationToken = default)
            where T : class, ISettingsConfig, new();

        /// <summary>
        /// Raised when settings are externally changed.
        /// </summary>
        event Action<GeneralSettings> GeneralSettingsChanged;
    }
}
