// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.PowerToys.Settings.UI.Library.Interfaces
{
    /// <summary>
    /// Interface for asynchronous settings repository operations.
    /// Provides non-blocking access to settings with caching support.
    /// </summary>
    /// <typeparam name="T">The settings type.</typeparam>
    public interface IAsyncSettingsRepository<T>
        where T : class, ISettingsConfig, new()
    {
        /// <summary>
        /// Occurs when the settings have been externally changed (e.g., by another process).
        /// </summary>
        event Action<T> SettingsChanged;

        /// <summary>
        /// Gets the current cached settings synchronously.
        /// Returns the cached value immediately, or loads if not cached.
        /// </summary>
        T SettingsConfig { get; }

        /// <summary>
        /// Gets the settings asynchronously with optional refresh from disk.
        /// </summary>
        /// <param name="forceRefresh">If true, bypasses cache and reads from disk.</param>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>The settings object.</returns>
        ValueTask<T> GetSettingsAsync(bool forceRefresh = false, CancellationToken cancellationToken = default);

        /// <summary>
        /// Saves the settings asynchronously.
        /// </summary>
        /// <param name="settings">The settings to save.</param>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>A task representing the save operation.</returns>
        ValueTask SaveSettingsAsync(T settings, CancellationToken cancellationToken = default);

        /// <summary>
        /// Reloads the settings from disk, updating the cache.
        /// </summary>
        /// <returns>True if the reload was successful.</returns>
        bool ReloadSettings();

        /// <summary>
        /// Reloads the settings from disk asynchronously, updating the cache.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>True if the reload was successful.</returns>
        ValueTask<bool> ReloadSettingsAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Stops watching for external file changes.
        /// </summary>
        void StopWatching();

        /// <summary>
        /// Starts watching for external file changes.
        /// </summary>
        void StartWatching();
    }
}
