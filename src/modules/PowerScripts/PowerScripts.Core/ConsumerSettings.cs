// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Nodes;
using PowerScripts.Core.Storage;

namespace PowerScripts.Core;

public sealed record ConsumerState(string Id, bool UserEnabled, bool IsEnabled, bool CanChange, bool IsPolicyControlled, string? DisabledReason);

public sealed record ConsumerSettingsSnapshot(bool IsPowerScriptsEnabled, bool IsPowerScriptsPolicyControlled, IReadOnlyList<ConsumerState> Consumers);

/// <summary>Shared preference and policy resolution. Consumer identity is not authentication.</summary>
public sealed class ConsumerSettings
{
    private static readonly string[] KnownConsumers = ["advanced-paste", "keyboard-manager", "file-explorer"];
    private readonly ISettingsStore _store;
    private readonly Func<string, string, bool?> _readPolicy;
    private readonly Func<bool> _moduleEnabled;
    private readonly Func<bool> _legacyAdvancedPasteEnabled;

    public ConsumerSettings(
        ISettingsStore? store = null,
        Func<string, string, bool?>? readPolicy = null,
        Func<bool>? moduleEnabled = null,
        Func<bool>? legacyAdvancedPasteEnabled = null)
    {
        _store = store ?? SettingsStore.Current;
        _readPolicy = readPolicy ?? PowerScriptsPolicies.Read;
        _moduleEnabled = moduleEnabled ?? ModuleState.IsPowerScriptsEnabled;
        _legacyAdvancedPasteEnabled = legacyAdvancedPasteEnabled ?? ReadLegacyAdvancedPasteEnabled;
    }

    public ConsumerSettingsSnapshot GetSnapshot() => new(
        IsModuleEnabled(),
        PowerScriptsPolicies.ReadUtility("PowerScripts", _readPolicy).HasValue,
        KnownConsumers.Select(Get).ToArray());

    public ConsumerState Get(string consumerId)
    {
        ArgumentException.ThrowIfNullOrEmpty(consumerId);
        var config = LoadConfig();
        var preferences = GetPreferences(config);
        var userEnabled = preferences?[consumerId] is { } saved
            ? saved.GetValue<bool>()
            : consumerId switch
            {
                "advanced-paste" => _legacyAdvancedPasteEnabled(),
                "file-explorer" => false,
                _ => true,
            };
        var policy = _readPolicy(PowerScriptsPolicies.ConsumersKey, consumerId);
        var parent = consumerId switch
        {
            "advanced-paste" => "AdvancedPaste",
            "keyboard-manager" => "KeyboardManager",
            _ => null,
        };
        var parentDisabled = parent is not null && PowerScriptsPolicies.ReadUtility(parent, _readPolicy) == false;
        var moduleEnabled = IsModuleEnabled();
        var reason = !moduleEnabled ? "module-disabled" : parentDisabled ? "parent-policy-disabled" : null;
        return new ConsumerState(
            consumerId,
            userEnabled,
            reason is null && (policy ?? userEnabled),
            reason is null && !policy.HasValue,
            policy.HasValue || parentDisabled,
            reason);
    }

    public void Set(string consumerId, bool enabled)
    {
        if (!KnownConsumers.Contains(consumerId, StringComparer.Ordinal))
        {
            throw new ArgumentException("Only registered consumer preferences can be changed.", nameof(consumerId));
        }

        var state = Get(consumerId);
        if (!state.CanChange)
        {
            throw new InvalidOperationException("The PowerScripts integration cannot be changed while its module is disabled or Group Policy controls it.");
        }

        var config = LoadConfig();
        var preferences = GetPreferences(config);
        if (preferences is null)
        {
            preferences = new JsonObject();
            config["consumers"] = preferences;
        }

        // Copy the legacy choice once, without changing the Advanced Paste settings schema.
        // Keyboard Manager stays enabled by default so existing bindings require no migration.
        preferences["advanced-paste"] ??= _legacyAdvancedPasteEnabled();
        preferences["keyboard-manager"] ??= true;
        preferences[consumerId] = enabled;
        _store.WriteBlob(PowerScriptsPaths.ConfigFileName, config.ToJsonString(new() { WriteIndented = true }));
    }

    private bool IsModuleEnabled() =>
        PowerScriptsPolicies.ReadUtility("PowerScripts", _readPolicy) ?? _moduleEnabled();

    private JsonObject LoadConfig()
    {
        var text = _store is FileSettingsStore files
            ? ReadOptionalFile(Path.Combine(files.BaseDirectory, PowerScriptsPaths.ConfigFileName))
            : _store.ReadBlob(PowerScriptsPaths.ConfigFileName);
        return text is null ? new JsonObject() : JsonNode.Parse(text) as JsonObject ??
            throw new InvalidDataException("PowerScripts config.json must contain a JSON object.");
    }

    private static JsonObject? GetPreferences(JsonObject config) =>
        config["consumers"] is null ? null : config["consumers"] as JsonObject ??
            throw new InvalidDataException("PowerScripts consumer preferences must contain a JSON object.");

    private bool ReadLegacyAdvancedPasteEnabled()
    {
        var moduleDirectory = _store is FileSettingsStore files ? files.BaseDirectory : PowerScriptsPaths.ModuleDirectory;
        var path = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(moduleDirectory))!, "AdvancedPaste", "settings.json");
        var text = ReadOptionalFile(path);
        if (text is null)
        {
            return false;
        }

        var settings = JsonNode.Parse(text) as JsonObject ??
            throw new InvalidDataException("Advanced Paste settings must contain a JSON object.");
        var saved = settings["properties"]?["powerscripts-enabled"];
        return saved is JsonObject property ? property["value"]?.GetValue<bool>() ?? false : saved?.GetValue<bool>() ?? false;
    }

    private static string? ReadOptionalFile(string path)
    {
        try
        {
            return File.ReadAllText(path);
        }
        catch (FileNotFoundException)
        {
            return null;
        }
        catch (DirectoryNotFoundException)
        {
            return null;
        }
    }
}
