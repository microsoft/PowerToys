// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using TopToolbar.Logging;
using TopToolbar.Services.Profiles.Models;

namespace TopToolbar.Services.Profiles;

public sealed class FileProfileStore : IProfileStore
{
    private readonly string _profilesDirectory;
    private readonly JsonSerializerOptions _jsonOptions;

    public FileProfileStore(string configRoot = null)
    {
        var root = string.IsNullOrWhiteSpace(configRoot)
            ? AppPaths.ConfigDirectory
            : configRoot;
        _profilesDirectory = Path.Combine(root, "profiles");
        Directory.CreateDirectory(_profilesDirectory);

        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        };
        _jsonOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
    }

    public ProfileOverridesFile Load(string profileId)
    {
        profileId = (profileId ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(profileId))
        {
            profileId = "default";
        }

        var path = GetProfilePath(profileId);
        try
        {
            if (!File.Exists(path))
            {
                var created = CreateEmpty(profileId);
                Save(created);
                return created;
            }

            using var stream = File.OpenRead(path);
            var loaded = JsonSerializer.Deserialize<ProfileOverridesFile>(stream, _jsonOptions) ?? CreateEmpty(profileId);
            Normalize(loaded);
            loaded.ProfileId = profileId; // enforce
            return loaded;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ProfileStore: failed to load '{profileId}' - {ex.Message}; recreating.");
            var recreated = CreateEmpty(profileId);
            Save(recreated);
            return recreated;
        }
    }

    public void Save(ProfileOverridesFile file)
    {
        Normalize(file);
        var path = GetProfilePath(file.ProfileId);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        // Create a temporary file first, then move to avoid corruption
        var tempPath = path + ".tmp";
        try
        {
            using (var stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.Read))
            {
                JsonSerializer.Serialize(stream, file, _jsonOptions);
            }

            // Atomic move operation
            if (File.Exists(path))
            {
                File.Delete(path);
            }

            File.Move(tempPath, path);
        }
        catch
        {
            // Clean up temp file if something goes wrong
            if (File.Exists(tempPath))
            {
                try
                {
                    File.Delete(tempPath);
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }

            throw;
        }
    }

    private string GetProfilePath(string profileId) => Path.Combine(_profilesDirectory, profileId + ".json");

    private static ProfileOverridesFile CreateEmpty(string id) => new() { ProfileId = id, Overrides = new ProfileOverrides() };

    private static void Normalize(ProfileOverridesFile file)
    {
        file.ProfileId = file.ProfileId?.Trim() ?? string.Empty;
        file.Overrides ??= new ProfileOverrides();
        file.Overrides.Groups ??= new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        file.Overrides.Actions ??= new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

        // Remove empty/whitespace keys
        foreach (var k in file.Overrides.Groups.Keys.Where(k => string.IsNullOrWhiteSpace(k)).ToList())
        {
            file.Overrides.Groups.Remove(k);
        }

        foreach (var k in file.Overrides.Actions.Keys.Where(k => string.IsNullOrWhiteSpace(k)).ToList())
        {
            file.Overrides.Actions.Remove(k);
        }
    }
}
