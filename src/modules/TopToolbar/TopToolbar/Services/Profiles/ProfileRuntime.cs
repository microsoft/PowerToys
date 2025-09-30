// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TopToolbar.Models;
using TopToolbar.Models.Abstractions;

namespace TopToolbar.Services.Profiles
{
    /// <summary>
    /// Concrete implementation managing active profile state and switching logic.
    /// Responsibility: load profiles from disk (via ProfileFileService) and expose active selection.
    /// Does NOT perform implicit migration; relies on existing files as-is.
    /// </summary>
    public sealed class ProfileRuntime : IProfileRuntime
    {
        private readonly string _dataDirectory;
        private readonly ProfileFileService _fileService;
        private readonly TimeSpan _cacheExpiry = TimeSpan.FromMinutes(5); // Cache for 5 minutes

        private bool _initialized;
        private Profile _activeProfile;
        private string _activeProfileId = "default"; // initial desired id; may not exist
        private bool _disposed;

        // Cache for all profiles to avoid repeated file I/O
        private IReadOnlyList<IProfile> _cachedAllProfiles;
        private DateTime _cacheLastUpdated = DateTime.MinValue;

        public ProfileRuntime(string dataDirectory = null, ProfileFileService fileService = null)
        {
            _dataDirectory = dataDirectory ?? GetDefaultDataDirectory();
            Directory.CreateDirectory(_dataDirectory);
            _fileService = fileService ?? new ProfileFileService(_dataDirectory);
        }

        public string ActiveProfileId => _activeProfileId;

        public IProfile ActiveProfile => _activeProfile;

        public ProfileFileService FileService => _fileService;

        public event EventHandler<IProfile> ActiveProfileChanged;

        public async Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            if (_initialized)
            {
                return;
            }

            await Task.Run(
                () =>
            {
                // Attempt to load desired active profile; if missing, leave null
                _activeProfile = _fileService.GetProfile(_activeProfileId);
                if (_activeProfile == null)
                {
                    System.Diagnostics.Debug.WriteLine($"ProfileRuntime: active profile '{_activeProfileId}' not found; remaining without active profile.");
                }

                _initialized = true;
            },
                cancellationToken).ConfigureAwait(false);

            ActiveProfileChanged?.Invoke(this, _activeProfile);
        }

        public bool Switch(string profileId)
        {
            if (string.IsNullOrWhiteSpace(profileId) || string.Equals(profileId, _activeProfileId, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var newProfile = _fileService.GetProfile(profileId);
            if (newProfile == null)
            {
                return false; // not found
            }

            _activeProfileId = profileId;
            _activeProfile = newProfile;
            ActiveProfileChanged?.Invoke(this, _activeProfile);
            return true;
        }

        /// <summary>
        /// Notify that the current active profile has been updated (content changed, not profile switch).
        /// This will reload the active profile from disk and notify listeners.
        /// </summary>
        public void NotifyActiveProfileUpdated()
        {
            if (string.IsNullOrWhiteSpace(_activeProfileId))
            {
                System.Diagnostics.Debug.WriteLine("ProfileRuntime.NotifyActiveProfileUpdated: No active profile ID");
                return;
            }

            System.Diagnostics.Debug.WriteLine($"ProfileRuntime.NotifyActiveProfileUpdated: Reloading profile '{_activeProfileId}'");

            // Reload the current active profile from disk
            var updatedProfile = _fileService.GetProfile(_activeProfileId);
            if (updatedProfile != null)
            {
                _activeProfile = updatedProfile;
                System.Diagnostics.Debug.WriteLine($"ProfileRuntime.NotifyActiveProfileUpdated: Profile reloaded, notifying {ActiveProfileChanged?.GetInvocationList()?.Length ?? 0} listeners");
                ActiveProfileChanged?.Invoke(this, _activeProfile);
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"ProfileRuntime.NotifyActiveProfileUpdated: Failed to reload profile '{_activeProfileId}'");
            }
        }

        public IReadOnlyList<IProfile> GetAllProfiles()
        {
            try
            {
                // Check if cache is valid
                if (_cachedAllProfiles != null &&
                    DateTime.UtcNow - _cacheLastUpdated < _cacheExpiry)
                {
                    System.Diagnostics.Debug.WriteLine($"ProfileRuntime.GetAllProfiles: Using cached profiles ({_cachedAllProfiles.Count} items)");
                    return _cachedAllProfiles;
                }

                System.Diagnostics.Debug.WriteLine("ProfileRuntime.GetAllProfiles: Loading profiles from disk");

                // Load fresh data from disk
                var profiles = _fileService.GetAllProfiles().Cast<IProfile>().ToList();

                // Update cache
                _cachedAllProfiles = profiles;
                _cacheLastUpdated = DateTime.UtcNow;

                System.Diagnostics.Debug.WriteLine($"ProfileRuntime.GetAllProfiles: Cached {profiles.Count} profiles");
                return _cachedAllProfiles;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ProfileRuntime.GetAllProfiles: Error loading profiles: {ex.Message}");
                return Array.Empty<IProfile>();
            }
        }

        private static string GetDefaultDataDirectory()
        {
                return AppPaths.ProfilesDirectory;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _fileService?.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
