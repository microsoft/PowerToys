// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Text.Json;

namespace FileExplorerScripts;

internal static class ExplorerSettings
{
    public static string DirectoryPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "PowerToys", "File Explorer");

    public static string CachePath => Path.Combine(DirectoryPath, "PowerScripts", "catalogue.json");

    public static string RequestsPath => Path.Combine(DirectoryPath, "PowerScripts", "Requests");

    public static TimeSpan StatusDelay()
    {
        var path = Path.Combine(DirectoryPath, "settings.json");
        if (!File.Exists(path))
        {
            return TimeSpan.FromSeconds(2);
        }

        using var document = JsonDocument.Parse(File.ReadAllText(path));
        return ReadStatusDelay(document.RootElement);
    }

    internal static TimeSpan ReadStatusDelay(JsonElement root)
    {
        if (!root.TryGetProperty("properties", out var properties) ||
            !properties.TryGetProperty("powerscripts-status-delay-seconds", out var setting))
        {
            return TimeSpan.FromSeconds(2);
        }

        var value = setting.ValueKind == JsonValueKind.Object ? setting.GetProperty("value") : setting;
        if (!value.TryGetInt32(out var seconds) || seconds is < 0 or > 600)
        {
            throw new InvalidDataException("The PowerScripts status delay must be a whole number between 0 and 600 seconds.");
        }

        return TimeSpan.FromSeconds(seconds);
    }

    public static bool ShowParameters()
    {
        var path = Path.Combine(DirectoryPath, "settings.json");
        if (!File.Exists(path))
        {
            return true;
        }

        using var document = JsonDocument.Parse(File.ReadAllText(path));
        if (!document.RootElement.TryGetProperty("properties", out var properties) ||
            !properties.TryGetProperty("powerscripts-show-parameters", out var setting))
        {
            return true;
        }

        return (setting.ValueKind == JsonValueKind.Object ? setting.GetProperty("value") : setting).GetBoolean();
    }

    public static bool IsParentEnabled()
    {
        var path = Path.Combine(Path.GetDirectoryName(DirectoryPath)!, "settings.json");
        if (!File.Exists(path))
        {
            return true;
        }

        using var document = JsonDocument.Parse(File.ReadAllText(path));
        return !document.RootElement.TryGetProperty("enabled", out var enabled) ||
            !enabled.TryGetProperty("File Explorer Preview", out var value) || value.GetBoolean();
    }
}
