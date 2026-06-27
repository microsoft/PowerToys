// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using EnvironmentVariablesUILib.Models;

namespace EnvironmentVariablesUILib.Helpers
{
    public sealed class EnvironmentVariablesService : IEnvironmentVariablesService
    {
        private const string ProfilesJsonFileSubPath = "Microsoft\\PowerToys\\EnvironmentVariables\\";

        private readonly string _profilesJsonFilePath;

        private readonly SemaphoreSlim _fileAccessLock = new (1, 1);

        private int _disposed;

        private readonly IFileSystem _fileSystem;

        private readonly JsonSerializerOptions _serializerOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
        };

        public string ProfilesJsonFilePath => _profilesJsonFilePath;

        public EnvironmentVariablesService(IFileSystem fileSystem)
        {
            if (fileSystem == null)
            {
                throw new ArgumentNullException(nameof(fileSystem));
            }

            _fileSystem = fileSystem;

            var localApplicationDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (string.IsNullOrWhiteSpace(localApplicationDataPath))
            {
                localApplicationDataPath = Path.GetTempPath();
            }

            _profilesJsonFilePath = Path.Combine(localApplicationDataPath, ProfilesJsonFileSubPath, "profiles.json");
        }

        public void Dispose()
        {
            if (System.Threading.Interlocked.Exchange(ref _disposed, 1) == 1)
            {
                return;
            }

            var lockAcquired = false;
            try
            {
                _fileAccessLock.Wait();
                lockAcquired = true;
            }
            catch (ObjectDisposedException)
            {
                return;
            }
            finally
            {
                if (lockAcquired && _fileAccessLock.CurrentCount == 0)
                {
                    _fileAccessLock.Dispose();
                }
            }
        }

        public List<ProfileVariablesSet> ReadProfiles()
        {
            ThrowIfDisposed();
            _fileAccessLock.Wait();
            try
            {
                List<ProfileVariablesSet> profiles;
                try
                {
                    profiles = ReadProfilesFromPath(ProfilesJsonFilePath);
                    if (profiles != null)
                    {
                        return profiles;
                    }
                }
                catch (JsonException)
                {
                    var backupProfiles = ReadProfilesFromLatestBackup(out var backupPath);
                    if (backupProfiles != null && !string.IsNullOrWhiteSpace(backupPath))
                    {
                        RestoreProfilesJsonFromBackup(backupPath);
                    }

                    if (backupProfiles != null)
                    {
                        return backupProfiles;
                    }

                    return new List<ProfileVariablesSet>();
                }
                catch (Exception)
                {
                    var backupProfiles = ReadProfilesFromLatestBackup(out _);
                    if (backupProfiles != null)
                    {
                        return backupProfiles;
                    }

                    return new List<ProfileVariablesSet>();
                }

                return new List<ProfileVariablesSet>();
            }
            finally
            {
                _fileAccessLock.Release();
            }
        }

        public async Task WriteAsync(IEnumerable<ProfileVariablesSet> profiles)
        {
            ThrowIfDisposed();
            await _fileAccessLock.WaitAsync();
            try
            {
                CleanupStaleProfileJsonArtifacts();

                var persistedProfiles = SanitizeProfilesForPersistence(profiles);
                string jsonData = JsonSerializer.Serialize(persistedProfiles, _serializerOptions);

                var directoryPath = Path.GetDirectoryName(_profilesJsonFilePath);
                if (!string.IsNullOrWhiteSpace(directoryPath))
                {
                    _fileSystem.Directory.CreateDirectory(directoryPath);
                }

                var tempFilePath = $"{ProfilesJsonFilePath}.{Guid.NewGuid():N}.tmp";
                try
                {
                    await _fileSystem.File.WriteAllTextAsync(tempFilePath, jsonData);
                    if (_fileSystem.File.Exists(ProfilesJsonFilePath))
                    {
                        try
                        {
                            _fileSystem.File.Replace(tempFilePath, ProfilesJsonFilePath, null);
                        }
                        catch (Exception)
                        {
                            var backupFilePath = $"{ProfilesJsonFilePath}.{Guid.NewGuid():N}.bak";
                            try
                            {
                                _fileSystem.File.Copy(ProfilesJsonFilePath, backupFilePath);
                                _fileSystem.File.Delete(ProfilesJsonFilePath);
                                _fileSystem.File.Move(tempFilePath, ProfilesJsonFilePath);
                            }
                            catch (Exception)
                            {
                                if (_fileSystem.File.Exists(backupFilePath) && !_fileSystem.File.Exists(ProfilesJsonFilePath))
                                {
                                    _fileSystem.File.Move(backupFilePath, ProfilesJsonFilePath);
                                }

                                throw;
                            }
                            finally
                            {
                                 DeleteIfExists(backupFilePath);
                            }
                        }
                    }
                    else
                    {
                        _fileSystem.File.Move(tempFilePath, ProfilesJsonFilePath);
                    }
                }
                finally
                {
                    DeleteIfExists(tempFilePath);
                }
            }
            finally
            {
                _fileAccessLock.Release();
            }
        }
        private void ThrowIfDisposed()
        {
            if (System.Threading.Volatile.Read(ref _disposed) == 1)
            {
                throw new ObjectDisposedException(nameof(EnvironmentVariablesService));
            }
        }

        private void DeleteIfExists(string filePath)
        {
            try
            {
                if (_fileSystem.File.Exists(filePath))
                {
                    _fileSystem.File.Delete(filePath);
                }
            }
            catch
            {
            }
        }

        private List<ProfileVariablesSet> ReadProfilesFromPath(string filePath)
        {
            if (!_fileSystem.File.Exists(filePath))
            {
                return null;
            }

            var fileContent = _fileSystem.File.ReadAllText(filePath);
            if (string.IsNullOrWhiteSpace(fileContent))
            {
                return new List<ProfileVariablesSet>();
            }

            var profiles = JsonSerializer.Deserialize<List<ProfileVariablesSet>>(fileContent);
            return profiles ?? new List<ProfileVariablesSet>();
        }

        private List<ProfileVariablesSet> ReadProfilesFromLatestBackup(out string backupPath)
        {
            backupPath = null;
            var directoryPath = Path.GetDirectoryName(_profilesJsonFilePath);
            if (string.IsNullOrWhiteSpace(directoryPath) || !_fileSystem.Directory.Exists(directoryPath))
            {
                return null;
            }

            try
            {
                var baseFileName = Path.GetFileName(ProfilesJsonFilePath);
                var wildcardPattern = $"{baseFileName}.*.bak";

                var backupPaths = _fileSystem.Directory.EnumerateFiles(directoryPath, wildcardPattern)
                    .Where(filePath => IsProfileArtifact(filePath, baseFileName, ".bak"))
                    .OrderByDescending(filePath => _fileSystem.File.GetLastWriteTimeUtc(filePath));

                foreach (var backupCandidatePath in backupPaths)
                {
                    try
                    {
                        var profiles = ReadProfilesFromPath(backupCandidatePath);
                        if (profiles != null)
                        {
                            backupPath = backupCandidatePath;
                            return profiles;
                        }
                    }
                    catch (JsonException)
                    {
                    }
                    catch (Exception)
                    {
                    }
                }
            }
            catch
            {
            }

            return null;
        }

        private void RestoreProfilesJsonFromBackup(string backupPath)
        {
            try
            {
                var directoryPath = Path.GetDirectoryName(_profilesJsonFilePath);
                if (!string.IsNullOrWhiteSpace(directoryPath))
                {
                    _fileSystem.Directory.CreateDirectory(directoryPath);
                }

                _fileSystem.File.Copy(backupPath, ProfilesJsonFilePath, true);
                DeleteIfExists(backupPath);
            }
            catch
            {
            }
        }

        private static bool IsProfileArtifact(string filePath, string baseFileName, string extension)
        {
            var fileName = Path.GetFileName(filePath);
            if (!fileName.StartsWith(baseFileName + ".", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (!Path.GetExtension(filePath).Equals(extension, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var artifactIdLength = fileName.Length - baseFileName.Length - 1 - extension.Length;
            if (artifactIdLength != 32)
            {
                return false;
            }

            var artifactId = fileName.Substring(baseFileName.Length + 1, artifactIdLength);
            return Guid.TryParseExact(artifactId, "N", out _);
        }

        private void CleanupStaleProfileJsonArtifacts()
        {
            var directoryPath = Path.GetDirectoryName(_profilesJsonFilePath);
            if (string.IsNullOrWhiteSpace(directoryPath) || !_fileSystem.Directory.Exists(directoryPath))
            {
                return;
            }

            try
            {
                var baseFileName = Path.GetFileName(ProfilesJsonFilePath);
                var wildcardPattern = $"{baseFileName}.*";

                foreach (var filePath in _fileSystem.Directory.EnumerateFiles(directoryPath, wildcardPattern))
                {
                    var extension = Path.GetExtension(filePath);
                    if (extension.Equals(".tmp", StringComparison.OrdinalIgnoreCase)
                        || extension.Equals(".bak", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!IsProfileArtifact(filePath, baseFileName, extension))
                        {
                            continue;
                        }

                        DeleteIfExists(filePath);
                    }
                }
            }
            catch
            {
            }
        }

        private static List<ProfileVariablesSet> SanitizeProfilesForPersistence(IEnumerable<ProfileVariablesSet> profiles)
        {
            var result = new List<ProfileVariablesSet>();
            if (profiles == null)
            {
                return result;
            }

            var profileIds = new HashSet<Guid>();
            var profileNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var profile in profiles.Where(p => p != null && p.Id != Guid.Empty))
            {
                if (!profileIds.Add(profile.Id))
                {
                    continue;
                }

                var profileName = (profile.Name ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(profileName))
                {
                    continue;
                }

                if (!profileNames.Add(profileName))
                {
                    continue;
                }

                var persistedVariables = profile.Variables == null
                    ? new List<Variable>()
                    : SanitizeProfileVariables(profile.Variables);

                var persistedProfile = new ProfileVariablesSet(profile.Id, profileName)
                {
                    IsEnabled = profile.IsEnabled,
                    Variables = new ObservableCollection<Variable>(persistedVariables),
                };

                result.Add(persistedProfile);
            }

            return result
                .OrderBy(profile => (profile.Name ?? string.Empty).Trim(), StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static List<Variable> SanitizeProfileVariables(IEnumerable<Variable> variables)
        {
            var persistedVariables = new List<Variable>();
            var variableNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (variables == null)
            {
                return persistedVariables;
            }

            foreach (var variable in variables)
            {
                if (variable == null)
                {
                    continue;
                }

                var variableName = variable.Name?.Trim();
                if (string.IsNullOrWhiteSpace(variableName))
                {
                    continue;
                }

                if (!variableNames.Add(variableName))
                {
                    continue;
                }

                var variableClone = variable.Clone();
                variableClone.Name = variableName;
                variableClone.ParentType = VariablesSetType.Profile;
                variableClone.ApplyToSystem = variable.ApplyToSystem;

                persistedVariables.Add(variableClone);
            }

            return persistedVariables
                .OrderBy(variable => (variable.Name ?? string.Empty).Trim(), StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
    }
}
