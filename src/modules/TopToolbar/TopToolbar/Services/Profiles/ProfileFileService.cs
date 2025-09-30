// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using TopToolbar.Logging;
using TopToolbar.Models;
using TopToolbar.Services.Profiles.Models;

namespace TopToolbar.Services.Profiles;

/// <summary>
/// Service to manage individual profile files containing complete profile configurations
/// Profile list is read from profiles/profiles.json
/// Individual profile configs are stored in current directory as {profileId}.json
/// </summary>
public class ProfileFileService : IDisposable
{
    private readonly string _configDirectory; // Current directory for {id}.json files
    private readonly IProfileRegistry _profileRegistry; // For reading profiles.json
    private readonly JsonSerializerOptions _jsonOptions;
    private bool _disposed;

    public ProfileFileService(string dataDirectory = null, IProfileRegistry profileRegistry = null)
    {
        _configDirectory = dataDirectory ?? GetDefaultDataDirectory();
        _profileRegistry = profileRegistry ?? new FileProfileRegistry();
        Directory.CreateDirectory(_configDirectory);

        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };
    }

    private static string GetDefaultDataDirectory()
    {
        return AppPaths.ProfilesDirectory;
    }

    public IReadOnlyList<Profile> GetAllProfiles()
    {
        try
        {
            // Get profile list from profiles.json via ProfileRegistry
            var registry = _profileRegistry.Load();
            var profiles = new List<Profile>();
            foreach (var profileMeta in registry.Profiles)
            {
                var configFilePath = GetProfileFilePath(profileMeta.Id);
                if (!File.Exists(configFilePath))
                {
                    // Missing file: DO NOT auto-create; skip
                    continue;
                }

                var loaded = LoadProfileFromFile(configFilePath);
                if (loaded != null)
                {
                    profiles.Add(loaded);
                }
            }

            return profiles
                .OrderBy(p => p.Name)
                .ToList();
        }
        catch (Exception ex)
        {
            // Use simple logging instead of AppLogger class
            System.Diagnostics.Debug.WriteLine($"Failed to load profiles: {ex.Message}");
            return new List<Profile>();
        }
    }

    public Profile GetProfile(string profileId)
    {
        if (string.IsNullOrWhiteSpace(profileId))
        {
            return null;
        }

        var filePath = GetProfileFilePath(profileId);
        if (!File.Exists(filePath))
        {
            return null; // no implicit creation
        }

        return LoadProfileFromFile(filePath);
    }

    public void SaveProfile(Profile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        if (string.IsNullOrWhiteSpace(profile.Id))
        {
            throw new ArgumentException("Profile Id cannot be empty", nameof(profile));
        }

        try
        {
            profile.ModifiedAt = DateTime.UtcNow;
            var filePath = GetProfileFilePath(profile.Id);
            var json = JsonSerializer.Serialize(profile, _jsonOptions);
            File.WriteAllText(filePath, json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to save profile {profile.Id}: {ex.Message}");
            throw;
        }
    }

    public void DeleteProfile(string profileId)
    {
        if (string.IsNullOrWhiteSpace(profileId) || string.Equals(profileId, "default", StringComparison.OrdinalIgnoreCase))
        {
            return; // Don't delete default profile
        }

        try
        {
            var filePath = GetProfileFilePath(profileId);
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to delete profile {profileId}: {ex.Message}");
            throw;
        }
    }

    public Profile CreateEmptyProfile(string profileId, string name)
    {
        if (string.IsNullOrWhiteSpace(profileId))
        {
            profileId = Guid.NewGuid().ToString();
        }

        var profile = new Profile
        {
            Id = profileId,
            Name = string.IsNullOrWhiteSpace(name) ? $"Profile {DateTime.Now:HH:mm}" : name,
            Description = "A new profile with custom actions",
            CreatedAt = DateTime.UtcNow,
            ModifiedAt = DateTime.UtcNow,
            Groups = new List<ProfileGroup>(), // Empty groups list
        };

        // Ensure workspace groups are added to new profiles
        EnsureWorkspaceGroupsSync(profile);

        return profile;
    }

    // Explicit creation API (callers must add to registry if they want persistence in list)
    public Profile CreateAndPersistProfile(string profileId, string name)
    {
        var profile = CreateEmptyProfile(profileId, name);
        SaveProfile(profile);

        // Also add to registry if missing
        try
        {
            var reg = _profileRegistry.Load();
            if (!reg.Profiles.Any(p => string.Equals(p.Id, profile.Id, StringComparison.OrdinalIgnoreCase)))
            {
                reg.Profiles.Add(new ProfileMeta { Id = profile.Id, Name = profile.Name });
                _profileRegistry.Save(reg);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to update registry for new profile {profile.Id}: {ex.Message}");
        }

        return profile;
    }

    private Profile LoadProfileFromFile(string filePath)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                return null;
            }

            var json = File.ReadAllText(filePath);
            var profile = JsonSerializer.Deserialize<Profile>(json, _jsonOptions);

            // Ensure workspace groups exist with all actions enabled
            if (profile != null)
            {
                EnsureWorkspaceGroupsSync(profile);
            }

            return profile;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load profile from {filePath}: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Ensures that built-in provider groups (workspace and MCP) exist in the profile.
    /// If they don't exist, creates them with all actions enabled.
    /// If they exist, preserves their current enabled/disabled states.
    /// </summary>
    private async System.Threading.Tasks.Task EnsureWorkspaceGroupsAsync(Profile profile)
    {
        if (profile?.Groups == null)
        {
            return;
        }

        try
        {
            // Get default built-in provider groups (workspace and MCP providers)
            using var builtinProvider = new Providers.BuiltinProvider();
            var defaultGroups = await builtinProvider.GetDefaultProfileGroupsAsync();

            foreach (var defaultGroup in defaultGroups)
            {
                // Check if this group already exists in the profile
                var existingGroup = profile.Groups.FirstOrDefault(g =>
                    string.Equals(g.Id, defaultGroup.Id, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(g.Name, defaultGroup.Name, StringComparison.OrdinalIgnoreCase));

                if (existingGroup == null)
                {
                    // Group doesn't exist, add it with all actions enabled (from built-in provider defaults)
                    profile.Groups.Add(defaultGroup);
                }

                // If group exists, preserve its current enabled/disabled states (don't modify)
            }
        }
        catch (Exception ex)
        {
            // Log error but don't fail profile loading
            try
            {
                System.Diagnostics.Debug.WriteLine($"ProfileFileService: Failed to ensure workspace groups: {ex.Message}");
            }
            catch
            {
                // Ignore logging errors
            }
        }
    }

    /// <summary>
    /// Synchronous wrapper for EnsureWorkspaceGroupsAsync to avoid breaking existing call chains.
    /// </summary>
    private void EnsureWorkspaceGroupsSync(Profile profile)
    {
        try
        {
            var task = EnsureWorkspaceGroupsAsync(profile);
            task.Wait();
        }
        catch (Exception ex)
        {
            // Log error but don't fail profile loading
            System.Diagnostics.Debug.WriteLine($"ProfileFileService: Failed to ensure workspace groups: {ex.Message}");
        }
    }

    private string GetProfileFilePath(string profileId)
    {
        return Path.Combine(_configDirectory, $"{profileId}.json");
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            GC.SuppressFinalize(this);
        }
    }
}
