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

public sealed class FileProfileRegistry : IProfileRegistry
{
    private readonly string _registryPath;
    private readonly JsonSerializerOptions _jsonOptions;

    public FileProfileRegistry(string configRoot = null)
    {
        var root = string.IsNullOrWhiteSpace(configRoot)
            ? AppPaths.ProfilesDirectory
            : configRoot;
        Directory.CreateDirectory(root);
        _registryPath = Path.Combine(root, "profiles.json");

        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        };
        _jsonOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
    }

    public ProfilesRegistry Load()
    {
        try
        {
            if (!File.Exists(_registryPath))
            {
                var reg = CreateDefault();
                Save(reg);
                return reg;
            }

            using var stream = File.OpenRead(_registryPath);
            var loaded = JsonSerializer.Deserialize<ProfilesRegistry>(stream, _jsonOptions) ?? CreateDefault();
            Normalize(loaded);

            var needsSave = false;

            if (!loaded.Profiles.Any(p => string.Equals(p.Id, loaded.ActiveProfileId, StringComparison.OrdinalIgnoreCase)))
            {
                loaded.ActiveProfileId = "default";
                needsSave = true;
            }

            if (!loaded.Profiles.Any(p => p.Id.Equals("default", StringComparison.OrdinalIgnoreCase)))
            {
                loaded.Profiles.Insert(0, new ProfileMeta { Id = "default", Name = "Default" });
                needsSave = true;
            }

            // Only save if we made changes
            if (needsSave)
            {
                Save(loaded);
            }

            return loaded;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ProfileRegistry: load failed - {ex.Message}; recreating.");
            var reg = CreateDefault();
            Save(reg);
            return reg;
        }
    }

    public void Save(ProfilesRegistry registry)
    {
        Normalize(registry);

        // Create a temporary file first, then move to avoid corruption
        var tempPath = _registryPath + ".tmp";
        try
        {
            using (var stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.Read))
            {
                JsonSerializer.Serialize(stream, registry, _jsonOptions);
            }

            // Atomic move operation
            if (File.Exists(_registryPath))
            {
                File.Delete(_registryPath);
            }

            File.Move(tempPath, _registryPath);
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

    public void SetActive(string profileId)
    {
        var reg = Load();
        if (!reg.Profiles.Any(p => string.Equals(p.Id, profileId, StringComparison.OrdinalIgnoreCase)))
        {
            return; // silently ignore invalid id
        }

        reg.ActiveProfileId = profileId;
        Save(reg);
    }

    private static ProfilesRegistry CreateDefault()
    {
        return new ProfilesRegistry
        {
            ActiveProfileId = "default",
            Profiles = new List<ProfileMeta>
            {
                new ProfileMeta { Id = "default", Name = "Default" },
            },
        };
    }

    private static void Normalize(ProfilesRegistry reg)
    {
        reg.ActiveProfileId = reg.ActiveProfileId?.Trim() ?? "default";
        reg.Profiles ??= new List<ProfileMeta>();
        foreach (var p in reg.Profiles)
        {
            if (p == null)
            {
                continue;
            }

            p.Id = p.Id?.Trim() ?? string.Empty;
            p.Name = p.Name?.Trim() ?? p.Id;
        }

        reg.Profiles = reg.Profiles.Where(p => !string.IsNullOrWhiteSpace(p.Id)).DistinctBy(p => p.Id, StringComparer.OrdinalIgnoreCase).ToList();
    }
}
