// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;

namespace PowerScripts.Core;

/// <summary>
/// Well-known filesystem locations for the PowerScripts module. The scripts root can be overridden
/// (explicit path, environment variable, or a persisted user setting) which keeps tests and ad-hoc
/// runs hermetic and lets the user point PowerScripts at their own folder from Settings.
/// </summary>
public static class PowerScriptsPaths
{
    /// <summary>Environment variable that overrides the default scripts root.</summary>
    public const string RootEnvironmentVariable = "POWERSCRIPTS_ROOT";

    /// <summary>The folder a single script lives in must contain a file with this name.</summary>
    public const string ManifestFileName = "manifest.json";

    /// <summary>The user-settings file name persisted next to the module data.</summary>
    public const string ConfigFileName = "config.json";

    /// <summary>
    /// The module's data directory: <c>%LOCALAPPDATA%\Microsoft\PowerToys\PowerScripts</c>.
    /// </summary>
    public static string ModuleDirectory
    {
        get
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(localAppData, "Microsoft", "PowerToys", "PowerScripts");
        }
    }

    /// <summary>The user-settings file that persists the chosen scripts root.</summary>
    public static string ConfigFilePath => Path.Combine(ModuleDirectory, ConfigFileName);

    /// <summary>
    /// Default scripts root:
    /// <c>%LOCALAPPDATA%\Microsoft\PowerToys\PowerScripts\scripts</c>.
    /// </summary>
    public static string DefaultScriptsRoot => Path.Combine(ModuleDirectory, "scripts");

    /// <summary>
    /// Resolves the scripts root, honoring (in order): an explicit path, the environment override,
    /// the persisted user setting, then the default.
    /// </summary>
    public static string ResolveScriptsRoot(string? explicitRoot = null)
    {
        if (!string.IsNullOrWhiteSpace(explicitRoot))
        {
            return explicitRoot;
        }

        var fromEnv = Environment.GetEnvironmentVariable(RootEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(fromEnv))
        {
            return fromEnv;
        }

        var fromConfig = ReadConfiguredScriptsRoot();
        return string.IsNullOrWhiteSpace(fromConfig) ? DefaultScriptsRoot : fromConfig;
    }

    /// <summary>
    /// Reads the user-chosen scripts root from <see cref="ConfigFilePath"/>, or <c>null</c> if it is
    /// missing, empty, or unreadable.
    /// </summary>
    public static string? ReadConfiguredScriptsRoot()
    {
        try
        {
            if (!File.Exists(ConfigFilePath))
            {
                return null;
            }

            using var stream = File.OpenRead(ConfigFilePath);
            using var document = JsonDocument.Parse(stream);
            if (document.RootElement.TryGetProperty("scriptsRoot", out var value) &&
                value.ValueKind == JsonValueKind.String)
            {
                var root = value.GetString();
                return string.IsNullOrWhiteSpace(root) ? null : root;
            }
        }
        catch (Exception)
        {
            // A corrupt or unreadable config simply falls back to the default.
        }

        return null;
    }

    /// <summary>
    /// Persists the user-chosen scripts root to <see cref="ConfigFilePath"/>. Passing <c>null</c> or
    /// whitespace clears the override so the default is used again.
    /// </summary>
    public static void SaveConfiguredScriptsRoot(string? root)
    {
        Directory.CreateDirectory(ModuleDirectory);
        var normalized = string.IsNullOrWhiteSpace(root) ? string.Empty : root.Trim();
        var json = JsonSerializer.Serialize(new { scriptsRoot = normalized }, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(ConfigFilePath, json);
    }
}
