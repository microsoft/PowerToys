// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;
using System.Text.Json.Nodes;

namespace Microsoft.PowerToys.UITest.Next;

/// <summary>
/// Lightweight helpers for preparing PowerToys settings JSON before a test launches a module.
/// Reads/writes the JSON files directly with System.Text.Json so the harness keeps zero product
/// dependencies — unlike the legacy helper, which referenced <c>Settings.UI.Library</c>.
/// </summary>
public static class SettingsConfigHelper
{
    private static readonly JsonSerializerOptions Indented = new() { WriteIndented = true };
    private static readonly string[] KnownModuleNames =
    [
        "AdvancedPaste",
        "AlwaysOnTop",
        "Awake",
        "CmdNotFound",
        "CmdPal",
        "ColorPicker",
        "CropAndLock",
        "CursorWrap",
        "EnvironmentVariables",
        "FancyZones",
        "File Explorer Preview",
        "File Locksmith",
        "FindMyMouse",
        "GrabAndMove",
        "Hosts",
        "Image Resizer",
        "Keyboard Manager",
        "LightSwitch",
        "Measure Tool",
        "MouseHighlighter",
        "MouseJump",
        "MousePointerCrosshairs",
        "MouseWithoutBorders",
        "NewPlus",
        "Peek",
        "PowerDisplay",
        "PowerRename",
        "PowerToys Run",
        "QuickAccent",
        "RegistryPreview",
        "Shortcut Guide",
        "TextExtractor",
        "Workspaces",
        "ZoomIt",
    ];

    /// <summary>Root of the per-user PowerToys settings: <c>%LocalAppData%\Microsoft\PowerToys</c>.</summary>
    public static string PowerToysSettingsRoot => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Microsoft",
        "PowerToys");

    private static string GlobalSettingsPath => Path.Combine(PowerToysSettingsRoot, "settings.json");

    /// <summary>
    /// Snapshot a module's <c>settings.json</c> and restore its exact bytes on disposal, deleting the
    /// file instead when the test created it. Use before a suite mutates persistent profile settings.
    /// </summary>
    public static IDisposable PreserveModuleSettings(string moduleName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleName);
        return PreserveFile(Path.Combine(PowerToysSettingsRoot, moduleName, "settings.json"));
    }

    /// <summary>Snapshot an arbitrary file and restore its exact bytes or prior absence on disposal.</summary>
    public static IDisposable PreserveFile(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return new FileSnapshot(path);
    }

    internal static IDisposable PreserveFirstRunSettings() => PreserveFirstRunSettings(PowerToysSettingsRoot);

    internal static IDisposable PreserveFirstRunSettings(string settingsRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(settingsRoot);

        var globalSettings = PreserveFile(Path.Combine(settingsRoot, "settings.json"));
        try
        {
            var oobeSettings = PreserveFile(Path.Combine(settingsRoot, "oobe_settings.json"));
            return new CompositeSnapshot(globalSettings, oobeSettings);
        }
        catch
        {
            globalSettings.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Enable exactly the named modules in the global <c>settings.json</c> and disable every other
    /// known or already-listed module. Module names are the keys under <c>"enabled"</c>
    /// (e.g. "FancyZones", "ColorPicker", "Peek"). Creates the file and keys when missing.
    /// </summary>
    public static void ConfigureGlobalModuleSettings(params string[]? modulesToEnable)
    {
        modulesToEnable ??= Array.Empty<string>();
        Directory.CreateDirectory(PowerToysSettingsRoot);

        var root = File.Exists(GlobalSettingsPath)
            ? (JsonNode.Parse(File.ReadAllText(GlobalSettingsPath)) as JsonObject) ?? new JsonObject()
            : new JsonObject();

        ConfigureGlobalModuleSettings(root, modulesToEnable);
        File.WriteAllText(GlobalSettingsPath, root.ToJsonString(Indented));
    }

    internal static void ConfigureGlobalModuleSettings(JsonObject root, params string[] modulesToEnable)
    {
        if (root["enabled"] is not JsonObject enabled)
        {
            enabled = new JsonObject();
            root["enabled"] = enabled;
        }

        var requestedModules = modulesToEnable.ToHashSet(StringComparer.Ordinal);

        foreach (var key in enabled.Select(kv => kv.Key).ToList())
        {
            enabled[key] = requestedModules.Contains(key);
        }

        foreach (var module in KnownModuleNames)
        {
            enabled[module] = requestedModules.Contains(module);
        }

        foreach (var module in requestedModules)
        {
            enabled[module] = true;
        }
    }

    /// <summary>
    /// Suppress the first-run "Welcome to PowerToys" (OOBE) and "What's new" (SCOOBE) windows. On a
    /// fresh profile (e.g. a CI agent) the runner opens one of these centered, topmost windows, which
    /// steals centre-screen mouse gestures (a coordinate measurement at screen-centre lands on the
    /// Welcome window instead of the module overlay → empty result). Mirrors the runner's own gating:
    /// marks OOBE as already opened (<c>oobe_settings.json</c> → <c>openedAtFirstLaunch=true</c>) and
    /// disables the what's-new-after-updates setting (<c>settings.json</c> →
    /// <c>show_whats_new_after_updates=false</c>, which the runner honours regardless of version).
    /// Best-effort — never blocks a test from launching.
    /// </summary>
    public static void SuppressFirstRunExperience()
    {
        try
        {
            Directory.CreateDirectory(PowerToysSettingsRoot);

            // OOBE: mark as already opened so the runner skips the Welcome window.
            var oobe = new JsonObject { ["openedAtFirstLaunch"] = true };
            File.WriteAllText(Path.Combine(PowerToysSettingsRoot, "oobe_settings.json"), oobe.ToJsonString(Indented));

            // SCOOBE: disable "what's new after updates" (version-independent) in the general settings.
            var root = File.Exists(GlobalSettingsPath)
                ? (JsonNode.Parse(File.ReadAllText(GlobalSettingsPath)) as JsonObject) ?? new JsonObject()
                : new JsonObject();
            root["show_whats_new_after_updates"] = false;
            File.WriteAllText(GlobalSettingsPath, root.ToJsonString(Indented));
        }
        catch
        {
            // Best-effort — a fresh-run window is a nuisance, not a reason to fail the test setup.
        }
    }

    /// <summary>
    /// Update a module's <c>settings.json</c>
    /// (<c>%LocalAppData%\Microsoft\PowerToys\&lt;module&gt;\settings.json</c>). Seeds the file from
    /// <paramref name="defaultSettingsContent"/> when it doesn't exist, then applies
    /// <paramref name="updateSettingsAction"/> to the parsed object and writes it back.
    /// </summary>
    public static void UpdateModuleSettings(
        string moduleName,
        string defaultSettingsContent,
        Action<JsonObject> updateSettingsAction)
    {
        ArgumentNullException.ThrowIfNull(moduleName);
        ArgumentNullException.ThrowIfNull(updateSettingsAction);

        var moduleDir = Path.Combine(PowerToysSettingsRoot, moduleName);
        var settingsPath = Path.Combine(moduleDir, "settings.json");
        Directory.CreateDirectory(moduleDir);

        var existing = File.Exists(settingsPath) ? File.ReadAllText(settingsPath) : string.Empty;

        JsonObject settings;
        if (string.IsNullOrWhiteSpace(existing))
        {
            if (string.IsNullOrWhiteSpace(defaultSettingsContent))
            {
                throw new ArgumentException(
                    "Default settings content must be provided when the file doesn't exist.",
                    nameof(defaultSettingsContent));
            }

            settings = (JsonNode.Parse(defaultSettingsContent) as JsonObject)
                       ?? throw new InvalidOperationException($"Default settings for '{moduleName}' is not a JSON object.");
        }
        else
        {
            settings = (JsonNode.Parse(existing) as JsonObject)
                       ?? throw new InvalidOperationException($"Existing settings for '{moduleName}' is not a JSON object.");
        }

        updateSettingsAction(settings);

        File.WriteAllText(settingsPath, settings.ToJsonString(Indented));
    }

    private sealed class FileSnapshot : IDisposable
    {
        private readonly string path;
        private readonly bool existed;
        private readonly byte[]? content;
        private bool disposed;

        public FileSnapshot(string path)
        {
            this.path = path;
            existed = File.Exists(path);
            content = existed ? File.ReadAllBytes(path) : null;
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            if (existed)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllBytes(path, content!);
            }
            else
            {
                File.Delete(path);
            }
        }
    }

    private sealed class CompositeSnapshot(params IDisposable[] snapshots) : IDisposable
    {
        private bool disposed;

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            List<Exception>? failures = null;
            foreach (var snapshot in snapshots.Reverse())
            {
                try
                {
                    snapshot.Dispose();
                }
                catch (Exception ex)
                {
                    failures ??= [];
                    failures.Add(ex);
                }
            }

            if (failures is not null)
            {
                throw new AggregateException("One or more PowerToys settings files could not be restored.", failures);
            }
        }
    }
}
