// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace MonitorPower;

#pragma warning disable CA1305, CA1863

internal sealed class MonitorPowerProfilePersistence(string directory)
{
    private readonly string _directory = directory;

    public string GetPath(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName) ||
            !string.Equals(Path.GetFileName(fileName), fileName, StringComparison.Ordinal) ||
            !string.Equals(Path.GetExtension(fileName), ".json", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(MonitorPowerCore.Resources.profile_filename_invalid, nameof(fileName));
        }

        return Path.Combine(_directory, fileName);
    }

    public void Save(string fileName, MonitorPowerProfile profile, bool overwrite)
    {
        var path = GetPath(fileName);
        Directory.CreateDirectory(_directory);
        if (File.Exists(path) && !overwrite)
        {
            throw new IOException(string.Format(CultureInfo.CurrentCulture, MonitorPowerCore.Resources.profile_already_exists, profile.Name));
        }

        var tempPath = path + ".tmp";
        File.WriteAllText(tempPath, JsonSerializer.Serialize(profile, MonitorPowerProfileJsonContext.Default.MonitorPowerProfile));
        File.Move(tempPath, path, overwrite: true);
    }

    public MonitorPowerProfile? Load(string fileName)
    {
        var path = GetPath(fileName);
        if (!File.Exists(path))
        {
            return null;
        }

        return JsonSerializer.Deserialize(
            File.ReadAllText(path),
            MonitorPowerProfileJsonContext.Default.MonitorPowerProfile);
    }

    public IReadOnlyList<(string FileName, string Name)> List()
    {
        if (!Directory.Exists(_directory))
        {
            return [];
        }

        var profiles = new List<(string FileName, string Name)>();
        foreach (var path in Directory.GetFiles(_directory, "*.json"))
        {
            var fileName = Path.GetFileName(path);
            try
            {
                var profile = Load(fileName);
                if (profile is { Name.Length: > 0 })
                {
                    profiles.Add((fileName, profile.Name));
                }
            }
            catch (Exception ex) when (ex is IOException or JsonException or ArgumentException)
            {
                System.Diagnostics.Debug.WriteLine($"Skipping invalid Monitor Power profile '{fileName}': {ex.Message}");
            }
        }

        return profiles.OrderBy(profile => profile.Name, StringComparer.CurrentCultureIgnoreCase).ToArray();
    }

    public string? GetConflict(
        string name,
        IReadOnlyCollection<DisplayHelpers.DisplayTargetId> targets,
        string? excludedFileName = null)
    {
        var profiles = List()
            .Where(profile => !string.Equals(profile.FileName, excludedFileName, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        var nameConflict = profiles.FirstOrDefault(
            profile => string.Equals(profile.Name, name, StringComparison.OrdinalIgnoreCase));
        if (nameConflict != default)
        {
            return string.Format(CultureInfo.CurrentCulture, MonitorPowerCore.Resources.profile_name_already_exists, nameConflict.Name);
        }

        var targetSet = targets.ToHashSet();
        foreach (var profile in profiles)
        {
            var savedProfile = Load(profile.FileName);
            if (savedProfile != null && savedProfile.Targets.ToHashSet().SetEquals(targetSet))
            {
                return string.Format(CultureInfo.CurrentCulture, MonitorPowerCore.Resources.profile_combination_already_exists, savedProfile.Name);
            }
        }

        return null;
    }

    public void Delete(string fileName)
    {
        var path = GetPath(fileName);
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}
