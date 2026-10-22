// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using PowerScripts.Core.Manifest;
using PowerScripts.Core.Security;

namespace PowerScripts.Core.Storage;

/// <summary>Author defaults and the sparse, source-associated local configuration of one script.</summary>
public sealed class ScriptSettingsSnapshot
{
    public string EntryPath { get; init; } = string.Empty;

    public string SettingsPath { get; init; } = string.Empty;

    public bool HasDescriptor { get; init; }

    public bool HasLocalSettings { get; init; }

    public JsonObject Recommended { get; init; } = new();

    public JsonObject Effective { get; init; } = new();

    public JsonObject Overrides { get; init; } = new();

    public IReadOnlyList<string> ValidationErrors { get; init; } = Array.Empty<string>();

    public bool HasLocalMxcSettings { get; init; }

    public MxcScriptOverride? Mxc { get; init; }

    [JsonIgnore]
    public PowerScriptManifest Manifest { get; init; } = new();
}

/// <summary>
/// Versioned settings, one file per catalogue/source path, never written into a distributed package.
/// Reads are atomic; a per-record cross-process mutex coordinates read/modify/replace writes.
/// Missing sources remain registered (but unexecutable) until explicitly forgotten.
/// </summary>
public sealed class ScriptSettingsStore
{
    private readonly ISettingsStore _moduleSettings;

    public const int MaximumRecordBytes = 256 * 1024;

    public static JsonSerializerOptions JsonOptions { get; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        MaxDepth = 32,
    };

    public ScriptSettingsStore(string? directory = null, ISettingsStore? moduleSettings = null)
    {
        _moduleSettings = moduleSettings ?? SettingsStore.Current;
        DirectoryPath = Path.GetFullPath(directory ?? Path.Combine(
            (_moduleSettings as FileSettingsStore)?.BaseDirectory ?? PowerScriptsPaths.ModuleDirectory,
            "script-settings"));
    }

    public string DirectoryPath { get; }

    public string GetSettingsPath(string scriptsRoot, string entryPath) =>
        Path.Combine(GetRootDirectory(scriptsRoot), Hash(NormalizePath(entryPath)) + ".json");

    public ScriptSettingsSnapshot Get(string scriptsRoot, string entryPath)
    {
        var root = NormalizePath(scriptsRoot);
        var entry = NormalizePath(entryPath);
        return BuildSnapshot(root, entry, ReadRecord(root, entry));
    }

    /// <summary>
    /// Registers a bare script only on explicit save. Omitted request fields preserve that section;
    /// null MXC explicitly inherits global policy rather than resurrecting an old id-only grant.
    /// </summary>
    public ScriptSettingsSnapshot Save(string scriptsRoot, string entryPath, JsonObject request)
    {
        var root = NormalizePath(scriptsRoot);
        var entry = NormalizePath(entryPath);
        return WithWriteLock(root, entry, () =>
        {
            var record = ReadRecord(root, entry) ?? new ScriptSettingsRecord { ScriptsRoot = root, EntryPath = entry };
            if (request.TryGetPropertyValue("overrides", out var overrides))
            {
                if (!record.HasMxcOverride)
                {
                    var previousId = BuildSnapshot(root, entry, record).Manifest.Id;
                    var legacy = MxcSettings.Load(_moduleSettings).Scripts.GetValueOrDefault(previousId);
                    record.HasMxcOverride = true;
                    if (legacy is not null)
                    {
                        record.Mxc = legacy;
                        record.Mxc.RiskAccepted = false;
                    }
                }

                record.Overrides = overrides is JsonObject values
                    ? (JsonObject)values.DeepClone()
                    : throw new InvalidDataException("'overrides' must be an object; use {} to reset metadata.");
            }

            foreach (var key in request.Select(pair => pair.Key))
            {
                if (key is not ("overrides" or "mxc"))
                {
                    throw new InvalidDataException($"Unknown script settings section '{key}'.");
                }
            }

            var snapshot = BuildSnapshot(root, entry, record);
            if (snapshot.ValidationErrors.Count > 0)
            {
                throw new InvalidDataException(string.Join(Environment.NewLine, snapshot.ValidationErrors));
            }

            if (request.TryGetPropertyValue("mxc", out var mxc))
            {
                record.HasMxcOverride = true;
                record.Mxc = mxc is null ? null : mxc.Deserialize<MxcScriptOverride>(JsonOptions)
                    ?? throw new InvalidDataException("'mxc' must be an object or null.");
                ValidateMxc(record.Mxc);
                record.MxcApprovedHash = record.Mxc?.RiskAccepted == true ? ScriptIntegrity.ComputeHash(snapshot.Manifest) : null;
            }

            WriteRecord(root, entry, record);
            return BuildSnapshot(root, entry, record);
        });
    }

    public ScriptSettingsSnapshot Reset(string scriptsRoot, string entryPath)
    {
        var root = NormalizePath(scriptsRoot);
        var entry = NormalizePath(entryPath);
        return WithWriteLock(root, entry, () =>
        {
            // Explicit recovery/reset also works when the old record is corrupt. Retain a safe MXC
            // tombstone so removing overrides cannot revive a legacy, id-only relaxed policy.
            var record = new ScriptSettingsRecord { ScriptsRoot = root, EntryPath = entry, HasMxcOverride = true };
            var snapshot = BuildSnapshot(root, entry, record);
            WriteRecord(root, entry, record);
            return snapshot;
        });
    }

    public void Forget(string scriptsRoot, string entryPath)
    {
        var root = NormalizePath(scriptsRoot);
        var entry = NormalizePath(entryPath);
        WithWriteLock(root, entry, () =>
        {
            var snapshot = Get(root, entry);
            var legacy = MxcSettings.Load(_moduleSettings);
            var ids = new[] { snapshot.Manifest.Id, ReadString(snapshot.Recommended["name"]) };
            if (ids.Where(id => id is not null).Aggregate(false, (removed, id) => legacy.Scripts.Remove(id!) || removed))
            {
                legacy.Save(_moduleSettings);
            }

            File.Delete(GetSettingsPath(root, entry));
            return true;
        });
    }

    /// <summary>Enumerates registrations for this root only; never scans source/helper files.</summary>
    public IReadOnlyList<string> GetRegisteredEntries(string scriptsRoot, Action<string, string> reportError)
    {
        var root = NormalizePath(scriptsRoot);
        var directory = GetRootDirectory(root);
        var entries = new List<string>();
        if (!Directory.Exists(directory))
        {
            return entries;
        }

        foreach (var file in Directory.EnumerateFiles(directory, "*.json"))
        {
            try
            {
                var record = ReadFile(file);
                ValidateRecordIdentity(record, root, NormalizePath(record.EntryPath), file);
                entries.Add(record.EntryPath);
            }
            catch (Exception ex) when (IsSettingsError(ex))
            {
                reportError(file, $"Local script settings unavailable: {ex.Message}");
            }
        }

        return entries;
    }

    internal static bool IsSettingsError(Exception ex) =>
        ex is JsonException or InvalidDataException or IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException;

    private ScriptSettingsSnapshot BuildSnapshot(string root, string entry, ScriptSettingsRecord? record)
    {
        var descriptorPath = entry + ToolDescriptorParser.DescriptorSuffix;
        var hasDescriptor = File.Exists(descriptorPath);
        var recommended = hasDescriptor ? ReadObject(descriptorPath) : CreateBareDefaults(entry);
        NormalizePropertyNames(recommended, typeof(ToolDescriptor));
        NormalizeRecommendations(recommended, entry);
        var effective = (JsonObject)recommended.DeepClone();
        Merge(effective, record?.Overrides ?? new JsonObject());
        var errors = new List<string>();
        PowerScriptManifest? manifest = null;
        try
        {
            var descriptor = effective.Deserialize<ToolDescriptor>(JsonOptions);
            var runtime = descriptor?.PowerScript?.Runtime;
            if (runtime is not null &&
                !runtime.Equals("python", StringComparison.OrdinalIgnoreCase) &&
                !runtime.Equals("powershell", StringComparison.OrdinalIgnoreCase) &&
                !runtime.Equals("pwsh", StringComparison.OrdinalIgnoreCase))
            {
                errors.Add("Runtime must be PowerShell or Python.");
            }

            manifest = ToolDescriptorParser.Parse(descriptor, Path.GetFileName(entry));
            if (manifest is null)
            {
                errors.Add("The effective descriptor requires a name and valid input/output types.");
            }
        }
        catch (JsonException ex)
        {
            errors.Add($"Invalid effective script settings: {ex.Message}");
        }

        manifest ??= new PowerScriptManifest
        {
            Id = "unconfigured-" + Hash(entry)[..24],
            Name = ReadString(effective["title"]) ?? Path.GetFileName(entry),
            Entry = Path.GetFileName(entry),
        };
        manifest.FolderPath = Path.GetDirectoryName(entry)!;
        manifest.ScriptsRoot = root;
        manifest.HasDescriptor = hasDescriptor;
        manifest.HasLocalSettings = record is not null;
        manifest.HasLocalMxcSettings = record?.HasMxcOverride == true;
        if (!hasDescriptor || record?.Overrides.Count > 0)
        {
            manifest.LocalSettingsFingerprint = Canonicalize(effective).ToJsonString();
        }

        errors.AddRange(ManifestValidator.Validate(manifest, Path.GetFileName(manifest.FolderPath)));
        var (input, output) = ScriptIo.Resolve(manifest);
        manifest.InputFormat = input;
        manifest.OutputFormat = output;
        manifest.ConfigurationErrors = errors;
        var mxc = record?.Mxc is null ? null : JsonSerializer.Deserialize<MxcScriptOverride>(
            JsonSerializer.Serialize(record.Mxc, JsonOptions), JsonOptions);
        if (mxc?.RiskAccepted == true &&
            !string.Equals(record!.MxcApprovedHash, ScriptIntegrity.ComputeHash(manifest), StringComparison.Ordinal))
        {
            mxc.RiskAccepted = false;
        }

        manifest.LocalMxc = mxc;
        return new ScriptSettingsSnapshot
        {
            EntryPath = entry,
            SettingsPath = GetSettingsPath(root, entry),
            HasDescriptor = hasDescriptor,
            HasLocalSettings = record is not null,
            Recommended = recommended,
            Effective = effective,
            Overrides = (JsonObject)(record?.Overrides.DeepClone() ?? new JsonObject()),
            ValidationErrors = errors,
            HasLocalMxcSettings = manifest.HasLocalMxcSettings,
            Mxc = mxc,
            Manifest = manifest,
        };
    }

    private static JsonObject CreateBareDefaults(string entry)
    {
        var extension = Path.GetExtension(entry);
        var python = extension.Equals(".py", StringComparison.OrdinalIgnoreCase);
        if (!python && !extension.Equals(".ps1", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("Choose a .ps1 or .py script, or provide its .tool.json descriptor.");
        }

        var result = new JsonObject
        {
            ["name"] = "local-" + Hash(entry)[..32],
            ["title"] = Path.GetFileNameWithoutExtension(entry),
        };
        if (python)
        {
            result["x-execute"] = new JsonObject { ["command"] = new JsonArray("python", Path.GetFileName(entry)) };
        }

        return result;
    }

    private static void NormalizeRecommendations(JsonObject descriptor, string entry)
    {
        var parsed = ToolDescriptorParser.Parse(descriptor.Deserialize<ToolDescriptor>(JsonOptions), Path.GetFileName(entry));
        if (string.IsNullOrWhiteSpace(ReadString(descriptor["title"])))
        {
            descriptor["title"] = parsed?.Name ?? Path.GetFileNameWithoutExtension(entry);
        }

        descriptor["description"] ??= string.Empty;
        var extension = descriptor["x-powerscript"] as JsonObject ?? new JsonObject();
        descriptor["x-powerscript"] = extension.DeepClone();
        extension = (JsonObject)descriptor["x-powerscript"]!;
        extension["runtime"] ??= Path.GetExtension(entry).Equals(".py", StringComparison.OrdinalIgnoreCase) ? "python" : "powershell";
        var (input, output) = parsed is null
            ? (Execution.PowerScriptDataFormat.None, Execution.PowerScriptDataFormat.Text)
            : ScriptIo.Resolve(parsed);
        extension["input"] ??= ScriptIo.ToToken(input);
        extension["output"] ??= ScriptIo.ToToken(output);
        extension["extensions"] ??= JsonSerializer.SerializeToNode(parsed?.Input?.Extensions ?? new List<string>(), JsonOptions);
        extension["minFiles"] ??= parsed?.Input?.MinFiles ?? 1;
        extension["maxFiles"] ??= parsed?.Input?.MaxFiles ?? 0;
        extension["capabilities"] ??= new JsonArray();
        descriptor["inputSchema"] ??= new JsonObject { ["type"] = "object", ["properties"] = new JsonObject() };
    }

    private static void NormalizePropertyNames(JsonObject node, Type schema)
    {
        var properties = schema.GetProperties()
            .Select(property => (Property: property, Name: property.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name))
            .Where(pair => pair.Name is not null)
            .ToDictionary(pair => pair.Name!, pair => pair.Property, StringComparer.OrdinalIgnoreCase);
        var original = node.ToArray();
        node.Clear();
        foreach (var (key, value) in original)
        {
            if (!properties.TryGetValue(key, out var property))
            {
                node[key] = value?.DeepClone();
                continue;
            }

            var canonical = property.GetCustomAttribute<JsonPropertyNameAttribute>()!.Name;
            node[canonical] = value?.DeepClone();

            if (node[canonical] is JsonObject child)
            {
                if (property.PropertyType == typeof(Dictionary<string, JsonSchemaProperty>))
                {
                    foreach (var field in child.Select(pair => pair.Value).OfType<JsonObject>())
                    {
                        NormalizePropertyNames(field, typeof(JsonSchemaProperty));
                    }
                }
                else
                {
                    NormalizePropertyNames(child, property.PropertyType);
                }
            }
        }
    }

    private ScriptSettingsRecord? ReadRecord(string root, string entry)
    {
        var path = GetSettingsPath(root, entry);
        if (!File.Exists(path))
        {
            return null;
        }

        var record = ReadFile(path);
        ValidateRecordIdentity(record, root, entry, path);
        return record;
    }

    private static ScriptSettingsRecord ReadFile(string path)
    {
        var record = ReadObject(path).Deserialize<ScriptSettingsRecord>(JsonOptions)
            ?? throw new InvalidDataException("Script settings must be an object.");
        if (record.SchemaVersion != 1 || record.Overrides is null)
        {
            throw new InvalidDataException("Unsupported or invalid script settings schema.");
        }

        ValidateMxc(record.Mxc);
        return record;
    }

    private void ValidateRecordIdentity(ScriptSettingsRecord record, string root, string entry, string path)
    {
        if (!string.Equals(NormalizePath(record.ScriptsRoot), root, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(NormalizePath(record.EntryPath), entry, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(path, GetSettingsPath(root, entry), StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("Script settings do not match their catalogue and source path.");
        }
    }

    private static JsonObject ReadObject(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete);
        if (stream.Length > MaximumRecordBytes)
        {
            throw new InvalidDataException($"Script metadata/settings must not exceed {MaximumRecordBytes} bytes.");
        }

        return JsonNode.Parse(stream, documentOptions: new JsonDocumentOptions
        {
            AllowTrailingCommas = true,
            CommentHandling = JsonCommentHandling.Skip,
            MaxDepth = 32,
        }) as JsonObject ?? throw new InvalidDataException("Script metadata/settings must be a JSON object.");
    }

    private void WriteRecord(string root, string entry, ScriptSettingsRecord record)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(record, JsonOptions);
        if (bytes.Length > MaximumRecordBytes)
        {
            throw new InvalidDataException($"Script settings must not exceed {MaximumRecordBytes} bytes.");
        }

        var path = GetSettingsPath(root, entry);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                stream.Write(bytes);
                stream.Flush(flushToDisk: true);
            }

            if (File.Exists(path))
            {
                File.Replace(temporary, path, null);
            }
            else
            {
                File.Move(temporary, path);
            }
        }
        finally
        {
            File.Delete(temporary);
        }
    }

    private T WithWriteLock<T>(string root, string entry, Func<T> action)
    {
        using var mutex = new Mutex(false, @"Local\PowerToys.PowerScripts.Settings." + Hash(GetSettingsPath(root, entry)));
        var acquired = false;
        try
        {
            try
            {
                acquired = mutex.WaitOne(TimeSpan.FromSeconds(10));
            }
            catch (AbandonedMutexException)
            {
                acquired = true;
            }

            if (!acquired)
            {
                throw new IOException("Script settings are being updated by another process. Try again.");
            }

            return action();
        }
        finally
        {
            if (acquired)
            {
                mutex.ReleaseMutex();
            }
        }
    }

    private string GetRootDirectory(string root) => Path.Combine(DirectoryPath, Hash(NormalizePath(root))[..32]);

    private static string NormalizePath(string path) => Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));

    private static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value.ToUpperInvariant()))).ToLowerInvariant();

    private static string? ReadString(JsonNode? node) =>
        node is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;

    private static void Merge(JsonObject target, JsonObject overrides)
    {
        foreach (var (key, value) in overrides)
        {
            if (value is null)
            {
                target.Remove(key);
            }
            else if (value is JsonObject nested)
            {
                if (target[key] is not JsonObject existing)
                {
                    existing = new JsonObject();
                    target[key] = existing;
                }

                Merge(existing, nested);
            }
            else
            {
                target[key] = value?.DeepClone();
            }
        }
    }

    private static JsonNode Canonicalize(JsonNode? node) => node switch
    {
        JsonObject obj => new JsonObject(obj.OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => new KeyValuePair<string, JsonNode?>(pair.Key, pair.Value is null ? null : Canonicalize(pair.Value)))),
        JsonArray array => new JsonArray(array.Select(item => item is null ? null : Canonicalize(item)).ToArray()),
        _ => node!.DeepClone(),
    };

    private static void ValidateMxc(MxcScriptOverride? mxc)
    {
        if (mxc is not null &&
            (mxc.EnabledPolicies is null || mxc.DisabledPolicies is null ||
                mxc.EnabledPolicies.Concat(mxc.DisabledPolicies).Any(policy => policy is null || !MxcPolicies.IsKnown(policy))))
        {
            throw new InvalidDataException("MXC overrides contain an unknown or invalid restriction.");
        }
    }

    private sealed class ScriptSettingsRecord
    {
        public int SchemaVersion { get; set; } = 1;

        public string ScriptsRoot { get; set; } = string.Empty;

        public string EntryPath { get; set; } = string.Empty;

        public JsonObject Overrides { get; set; } = new();

        public bool HasMxcOverride { get; set; }

        public MxcScriptOverride? Mxc { get; set; }

        public string? MxcApprovedHash { get; set; }

        [JsonExtensionData]
        public Dictionary<string, JsonElement>? AdditionalData { get; set; }
    }
}
