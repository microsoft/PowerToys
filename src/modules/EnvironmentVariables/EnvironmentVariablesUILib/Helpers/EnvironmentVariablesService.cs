// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
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

            var fileContent = _fileSystem.File.ReadAllText(ProfilesJsonFilePath);
            if (string.IsNullOrWhiteSpace(fileContent))
            {
                return new List<ProfileVariablesSet>();
            }

            try
            {
                var profiles = JsonSerializer.Deserialize<List<ProfileVariablesSet>>(fileContent);
                return profiles ?? new List<ProfileVariablesSet>();
            }
            catch (JsonException)
            {
                return new List<ProfileVariablesSet>();
            }
        }

        public async Task WriteAsync(IEnumerable<ProfileVariablesSet> profiles)
        {
            string jsonData = JsonSerializer.Serialize(profiles, _serializerOptions);

            var directoryPath = Path.GetDirectoryName(_profilesJsonFilePath);
            if (!string.IsNullOrWhiteSpace(directoryPath))
            {
                _fileSystem.Directory.CreateDirectory(directoryPath);
            }

            await _fileSystem.File.WriteAllTextAsync(ProfilesJsonFilePath, jsonData);
        }
    }
}
