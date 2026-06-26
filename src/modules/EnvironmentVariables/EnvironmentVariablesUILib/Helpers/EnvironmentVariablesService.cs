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
using System.Threading.Tasks;

using EnvironmentVariablesUILib.Models;

namespace EnvironmentVariablesUILib.Helpers
{
    public sealed class EnvironmentVariablesService : IEnvironmentVariablesService
    {
        private const string ProfilesJsonFileSubPath = "Microsoft\\PowerToys\\EnvironmentVariables\\";

        private readonly string _profilesJsonFilePath;

        private readonly IFileSystem _fileSystem;

        private readonly JsonSerializerOptions _serializerOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
        };

        public string ProfilesJsonFilePath => _profilesJsonFilePath;

        public EnvironmentVariablesService(IFileSystem fileSystem)
        {
            _fileSystem = fileSystem;

            _profilesJsonFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), ProfilesJsonFileSubPath, "profiles.json");
        }

        public void Dispose()
        {
        }

        public List<ProfileVariablesSet> ReadProfiles()
        {
            if (!_fileSystem.File.Exists(ProfilesJsonFilePath))
            {
                return new List<ProfileVariablesSet>();
            }

            try
            {
                var fileContent = _fileSystem.File.ReadAllText(ProfilesJsonFilePath);
                if (string.IsNullOrWhiteSpace(fileContent))
                {
                    return new List<ProfileVariablesSet>();
                }

                var profiles = JsonSerializer.Deserialize<List<ProfileVariablesSet>>(fileContent);
                return profiles ?? new List<ProfileVariablesSet>();
            }
            catch (Exception)
            {
                return new List<ProfileVariablesSet>();
            }
        }

        public async Task WriteAsync(IEnumerable<ProfileVariablesSet> profiles)
        {
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
                    _fileSystem.File.Delete(ProfilesJsonFilePath);
                }

                _fileSystem.File.Move(tempFilePath, ProfilesJsonFilePath);
            }
            finally
            {
                if (_fileSystem.File.Exists(tempFilePath))
                {
                    _fileSystem.File.Delete(tempFilePath);
                }
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

                var persistedVariables = profile.Variables == null
                    ? new List<Variable>()
                    : profile.Variables
                        .Where(v => v != null && !string.IsNullOrWhiteSpace(v.Name))
                        .Select(v =>
                        {
                            var variableClone = v.Clone();
                            variableClone.Name = v.Name?.Trim();
                            variableClone.ParentType = VariablesSetType.Profile;
                            variableClone.ApplyToSystem = v.ApplyToSystem;
                            return variableClone;
                        })
                        .ToList();

                var persistedProfile = new ProfileVariablesSet(profile.Id, profileName)
                {
                    IsEnabled = profile.IsEnabled,
                    Variables = new ObservableCollection<Variable>(persistedVariables),
                };

                result.Add(persistedProfile);
            }

            return result;
        }
    }
}
