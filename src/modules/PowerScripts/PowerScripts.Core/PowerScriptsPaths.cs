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

    /// <summary>
    /// The PowerToys settings file (<c>%LOCALAPPDATA%\Microsoft\PowerToys\settings.json</c>) that
    /// records which modules are enabled. Read by <see cref="ModuleState"/> to gate execution.
    /// </summary>
    public static string PowerToysSettingsFilePath
    {
        get
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(localAppData, "Microsoft", "PowerToys", "settings.json");
        }
    }

    /// <summary>The user-settings file that persists the chosen scripts root.</summary>
    public static string ConfigFilePath => Path.Combine(ModuleDirectory, ConfigFileName);

    /// <summary>The trust store file name (records which script contents the user has approved).</summary>
    public const string TrustFileName = "trust.json";

    /// <summary>The trust store: which (script id, content hash) pairs the user has approved to run.</summary>
    public static string TrustFilePath => Path.Combine(ModuleDirectory, TrustFileName);

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
        var normalized = string.IsNullOrWhiteSpace(root) ? string.Empty : root.Trim();
        MergeConfigValue("scriptsRoot", JsonSerializer.SerializeToElement(normalized));
    }

    /// <summary>
    /// Reads the module's own enabled override from <see cref="ConfigFilePath"/>, or <c>null</c> when
    /// it is missing/unreadable. This is written by Settings and, unlike the PowerToys
    /// <c>settings.json</c> <c>enabled.PowerScripts</c> flag, it is owned solely by this module so it
    /// survives the runner rewriting <c>settings.json</c> (which drops entries for modules the runner
    /// does not itself host — as the prototype is not yet a registered runner module).
    /// </summary>
    public static bool? ReadEnabledOverride()
    {
        try
        {
            if (!File.Exists(ConfigFilePath))
            {
                return null;
            }

            using var stream = File.OpenRead(ConfigFilePath);
            using var document = JsonDocument.Parse(stream);
            if (document.RootElement.TryGetProperty("enabled", out var value) &&
                value.ValueKind is JsonValueKind.True or JsonValueKind.False)
            {
                return value.GetBoolean();
            }
        }
        catch (Exception)
        {
            // A corrupt or unreadable config simply yields no override.
        }

        return null;
    }

    /// <summary>Persists the module's enabled override to <see cref="ConfigFilePath"/>, preserving other keys.</summary>
    public static void SaveEnabled(bool enabled) => MergeConfigValue("enabled", JsonSerializer.SerializeToElement(enabled));

    /// <summary>Merges a single top-level key into <see cref="ConfigFilePath"/> without clobbering the rest.</summary>
    private static void MergeConfigValue(string key, JsonElement value)
    {
        Directory.CreateDirectory(ModuleDirectory);
        var options = new JsonSerializerOptions { WriteIndented = true };
        var merged = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        try
        {
            if (File.Exists(ConfigFilePath))
            {
                using var stream = File.OpenRead(ConfigFilePath);
                using var existing = JsonDocument.Parse(stream);
                foreach (var property in existing.RootElement.EnumerateObject())
                {
                    merged[property.Name] = property.Value.Clone();
                }
            }
        }
        catch (Exception)
        {
            // Overwrite a corrupt config rather than fail.
        }

        merged[key] = value;
        File.WriteAllText(ConfigFilePath, JsonSerializer.Serialize(merged, options));
    }
}
