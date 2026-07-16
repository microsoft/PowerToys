// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace PowerDisplay.Models
{
    internal sealed class ProfileStore
    {
        private readonly string _filePath;
        private readonly string _mutexName;
        private readonly TimeSpan _mutexTimeout;
        private readonly object _processLock = new object();

        internal ProfileStore(string filePath, string mutexName, TimeSpan mutexTimeout)
        {
            _filePath = filePath;
            _mutexName = mutexName;
            _mutexTimeout = mutexTimeout;
        }

        internal PowerDisplayProfiles LoadProfiles()
        {
            return ExecuteLocked(LoadProfilesCore);
        }

        internal Task<PowerDisplayProfiles> LoadProfilesAsync(CancellationToken cancellationToken = default)
            => RunAsync(LoadProfiles, cancellationToken);

        internal void SaveProfiles(PowerDisplayProfiles profiles)
        {
            ArgumentNullException.ThrowIfNull(profiles);
            ExecuteLocked(() => SaveProfilesCore(profiles));
        }

        internal void AddOrUpdateProfile(PowerDisplayProfile profile)
        {
            if (profile == null || !profile.IsValid())
            {
                throw new ArgumentException("Profile is invalid", nameof(profile));
            }

            ExecuteLocked(() =>
            {
                var profiles = LoadProfilesCore();
                var originalId = profile.Id;
                var originalLastModified = profile.LastModified;
                try
                {
                    profiles.SetProfile(profile);
                    SaveProfilesCore(profiles);
                }
                catch
                {
                    profile.Id = originalId;
                    profile.LastModified = originalLastModified;
                    throw;
                }
            });
        }

        internal Task AddOrUpdateProfileAsync(
            PowerDisplayProfile profile,
            CancellationToken cancellationToken = default)
            => RunAsync(
                () =>
                {
                    AddOrUpdateProfile(profile);
                    return true;
                },
                cancellationToken);

        internal bool RemoveProfileById(int id)
        {
            return ExecuteLocked(() =>
            {
                var profiles = LoadProfilesCore();
                if (!profiles.RemoveProfile(id))
                {
                    return false;
                }

                SaveProfilesCore(profiles);
                return true;
            });
        }

        internal Task<bool> RemoveProfileByIdAsync(int id, CancellationToken cancellationToken = default)
            => RunAsync(() => RemoveProfileById(id), cancellationToken);

        internal bool UpdateProfiles(Func<PowerDisplayProfiles, bool> update)
        {
            ArgumentNullException.ThrowIfNull(update);
            return ExecuteLocked(() =>
            {
                var profiles = LoadProfilesCore();
                if (!update(profiles))
                {
                    return false;
                }

                SaveProfilesCore(profiles);
                return true;
            });
        }

        internal Task<bool> UpdateProfilesAsync(
            Func<PowerDisplayProfiles, bool> update,
            CancellationToken cancellationToken = default)
            => RunAsync(() => UpdateProfiles(update), cancellationToken);

        private T ExecuteLocked<T>(Func<T> operation)
        {
            lock (_processLock)
            {
                using var mutex = new Mutex(initiallyOwned: false, _mutexName);
                var acquired = false;
                try
                {
                    try
                    {
                        acquired = mutex.WaitOne(_mutexTimeout);
                    }
                    catch (AbandonedMutexException)
                    {
                        acquired = true;
                    }

                    if (!acquired)
                    {
                        throw new TimeoutException($"Timed out waiting for the profile store mutex after {_mutexTimeout}.");
                    }

                    return operation();
                }
                finally
                {
                    if (acquired)
                    {
                        mutex.ReleaseMutex();
                    }
                }
            }
        }

        private void ExecuteLocked(Action operation)
        {
            ExecuteLocked(() =>
            {
                operation();
                return true;
            });
        }

        private static Task<T> RunAsync<T>(Func<T> operation, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(operation);
            return Task.Run(operation, cancellationToken);
        }

        private PowerDisplayProfiles LoadProfilesCore()
        {
            EnsureFolderExists();

            if (!File.Exists(_filePath))
            {
                return new PowerDisplayProfiles();
            }

            var json = File.ReadAllText(_filePath);
            return JsonSerializer.Deserialize(json, ProfileSerializationContext.Default.PowerDisplayProfiles)
                ?? throw new JsonException($"Profile file '{_filePath}' deserialized to null.");
        }

        private void SaveProfilesCore(PowerDisplayProfiles profiles)
        {
            EnsureFolderExists();
            var originalLastUpdated = profiles.LastUpdated;
            var temporaryPath = $"{_filePath}.{Guid.NewGuid():N}.tmp";

            try
            {
                profiles.LastUpdated = DateTime.UtcNow;
                var payload = JsonSerializer.SerializeToUtf8Bytes(
                    profiles,
                    ProfileSerializationContext.Default.PowerDisplayProfiles);
                using (var stream = new FileStream(
                    temporaryPath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None,
                    bufferSize: 4096,
                    options: FileOptions.WriteThrough))
                {
                    stream.Write(payload);
                    stream.Flush(flushToDisk: true);
                }

                File.Move(temporaryPath, _filePath, overwrite: true);
            }
            catch
            {
                profiles.LastUpdated = originalLastUpdated;
                try
                {
                    File.Delete(temporaryPath);
                }
                catch
                {
                    // Best-effort cleanup; the original persistence exception is rethrown.
                }

                throw;
            }
        }

        private void EnsureFolderExists()
        {
            var folder = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(folder))
            {
                Directory.CreateDirectory(folder);
            }
        }
    }
}
