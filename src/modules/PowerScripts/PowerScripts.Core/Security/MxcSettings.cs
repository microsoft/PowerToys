// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;
using System.Text.Json.Serialization;
using PowerScripts.Core.Storage;

namespace PowerScripts.Core.Security;

/// <summary>The stable PowerScripts policy names understood by the MXC integration.</summary>
public static class MxcPolicies
{
    public const string Filesystem = "filesystem";
    public const string Network = "network";
    public const string Ui = "ui";
    public const string LeastPrivilege = "leastPrivilege";

    public static IReadOnlyList<string> All { get; } =
        new[] { Filesystem, Network, Ui, LeastPrivilege };

    public static bool IsKnown(string policy) =>
        All.Contains(policy, StringComparer.OrdinalIgnoreCase);

    internal static List<string> Normalize(IEnumerable<string>? policies) =>
        (policies ?? Array.Empty<string>())
            .Where(IsKnown)
            .Select(p => All.First(known => known.Equals(p, StringComparison.OrdinalIgnoreCase)))
            .Distinct(StringComparer.Ordinal)
            .ToList();
}

/// <summary>An MXC override applying to one stable PowerScript id.</summary>
public sealed class MxcScriptOverride
{
    [JsonPropertyName("enabled")]
    public bool? Enabled { get; set; }

    [JsonPropertyName("disabledPolicies")]
    public List<string> DisabledPolicies { get; set; } = new();

    /// <summary>
    /// Restrictions explicitly enabled for this script, even when the global policy disables them.
    /// Enabling a restriction is always safe and does not require risk acceptance.
    /// </summary>
    [JsonPropertyName("enabledPolicies")]
    public List<string> EnabledPolicies { get; set; } = new();

    [JsonPropertyName("riskAccepted")]
    public bool RiskAccepted { get; set; }
}

/// <summary>
/// PowerScripts' MXC configuration, stored below the module <c>config.json</c> key <c>mxc</c>.
/// When no explicit enabled preference exists, MXC follows platform support: on for supported hosts
/// and off otherwise. Explicit user settings are preserved.
/// </summary>
public sealed class MxcSettings
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = true;

    [JsonIgnore]
    public bool UsesPlatformDefault { get; private set; }

    [JsonIgnore]
    public MxcPlatformSupportResult? PlatformSupport { get; private set; }

    /// <summary>Optional explicit path to <c>wxc-exec.exe</c>; empty means auto-discover.</summary>
    [JsonPropertyName("executorPath")]
    public string ExecutorPath { get; set; } = string.Empty;

    [JsonPropertyName("disabledPolicies")]
    public List<string> DisabledPolicies { get; set; } = new();

    [JsonPropertyName("riskAccepted")]
    public bool RiskAccepted { get; set; }

    /// <summary>Overrides keyed by the manifest's stable script id.</summary>
    [JsonPropertyName("scripts")]
    public Dictionary<string, MxcScriptOverride> Scripts { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Loads settings. A missing enabled preference follows the current host's MXC support probe.
    /// Explicit settings continue to require accepted risk before weakening isolation.
    /// </summary>
    public static MxcSettings Load(
        ISettingsStore? store = null,
        Func<MxcSettings, MxcPlatformSupportResult>? supportProbe = null)
    {
        store ??= SettingsStore.Current;
        supportProbe ??= MxcPlatformSupport.Probe;

        try
        {
            var text = store.ReadBlob(PowerScriptsPaths.ConfigFileName);
            if (string.IsNullOrWhiteSpace(text))
            {
                return CreatePlatformDefault(supportProbe);
            }

            using var document = JsonDocument.Parse(text);
            if (!document.RootElement.TryGetProperty("mxc", out var mxc) ||
                mxc.ValueKind != JsonValueKind.Object)
            {
                return CreatePlatformDefault(supportProbe);
            }

            var settings = Normalize(mxc.Deserialize<MxcSettings>(SerializerOptions));
            var hasExplicitEnabled = mxc.TryGetProperty("enabled", out var enabled) &&
                                     enabled.ValueKind is JsonValueKind.True or JsonValueKind.False;
            return hasExplicitEnabled ? settings : ApplyPlatformDefault(settings, supportProbe);
        }
        catch (JsonException)
        {
            return CreatePlatformDefault(supportProbe);
        }
        catch (NotSupportedException)
        {
            return CreatePlatformDefault(supportProbe);
        }
        catch (IOException)
        {
            return CreatePlatformDefault(supportProbe);
        }
        catch (UnauthorizedAccessException)
        {
            return CreatePlatformDefault(supportProbe);
        }
    }

    /// <summary>Persists the <c>mxc</c> section while preserving all other module settings.</summary>
    public void Save(ISettingsStore? store = null)
    {
        store ??= SettingsStore.Current;
        var root = new Dictionary<string, JsonElement>(StringComparer.Ordinal);

        try
        {
            var text = store.ReadBlob(PowerScriptsPaths.ConfigFileName);
            if (!string.IsNullOrWhiteSpace(text))
            {
                using var existing = JsonDocument.Parse(text);
                if (existing.RootElement.ValueKind == JsonValueKind.Object)
                {
                    foreach (var property in existing.RootElement.EnumerateObject())
                    {
                        root[property.Name] = property.Value.Clone();
                    }
                }
            }
        }
        catch (JsonException)
        {
            // Replacing a corrupt config with a safe, typed MXC section is preferable to retaining it.
        }
        catch (IOException)
        {
            // The subsequent write reports persistence failures to the caller.
        }
        catch (UnauthorizedAccessException)
        {
            // The subsequent write reports persistence failures to the caller.
        }

        root["mxc"] = SerializeForPersistence();
        store.WriteBlob(PowerScriptsPaths.ConfigFileName, JsonSerializer.Serialize(root, SerializerOptions));
    }

    private static MxcSettings Normalize(MxcSettings? settings)
    {
        if (settings is null)
        {
            return new MxcSettings();
        }

        settings.ExecutorPath ??= string.Empty;
        settings.DisabledPolicies = MxcPolicies.Normalize(settings.DisabledPolicies);

        var normalizedScripts = new Dictionary<string, MxcScriptOverride>(StringComparer.OrdinalIgnoreCase);
        if (settings.Scripts is not null)
        {
            foreach (var (id, scriptOverride) in settings.Scripts)
            {
                if (string.IsNullOrWhiteSpace(id) || scriptOverride is null)
                {
                    continue;
                }

                scriptOverride.DisabledPolicies = MxcPolicies.Normalize(scriptOverride.DisabledPolicies);
                scriptOverride.EnabledPolicies = MxcPolicies.Normalize(scriptOverride.EnabledPolicies);
                scriptOverride.DisabledPolicies.RemoveAll(policy =>
                    scriptOverride.EnabledPolicies.Contains(policy, StringComparer.Ordinal));
                normalizedScripts[id] = scriptOverride;
            }
        }

        settings.Scripts = normalizedScripts;
        return settings;
    }

    private static MxcSettings CreatePlatformDefault(
        Func<MxcSettings, MxcPlatformSupportResult> supportProbe) =>
        ApplyPlatformDefault(new MxcSettings(), supportProbe);

    private static MxcSettings ApplyPlatformDefault(
        MxcSettings settings,
        Func<MxcSettings, MxcPlatformSupportResult> supportProbe)
    {
        var support = supportProbe(settings);
        settings.Enabled = support.IsSupported;
        settings.UsesPlatformDefault = true;
        settings.PlatformSupport = support;
        return settings;
    }

    private JsonElement SerializeForPersistence()
    {
        var serialized = JsonSerializer.SerializeToElement(Normalize(this), SerializerOptions);
        if (!UsesPlatformDefault)
        {
            return serialized;
        }

        var properties = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var property in serialized.EnumerateObject())
        {
            if (!string.Equals(property.Name, "enabled", StringComparison.Ordinal))
            {
                properties[property.Name] = property.Value.Clone();
            }
        }

        return JsonSerializer.SerializeToElement(properties, SerializerOptions);
    }
}
