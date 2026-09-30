// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;
using System.Text.Json.Serialization;
using PowerScripts.Core.Storage;

namespace PowerScripts.Core.Security;

/// <summary>
/// A single trust-on-first-use record: the user approved a script id whose content matched
/// <see cref="Hash"/>. If the script's content or declared capabilities later change, the recomputed
/// hash no longer matches and the user is asked to approve again.
/// </summary>
public sealed class TrustRecord
{
    public string Id { get; set; } = string.Empty;

    public string Hash { get; set; } = string.Empty;

    public IReadOnlyList<string> Capabilities { get; set; } = [];

    public string? Source { get; set; }

    public string? Publisher { get; set; }

    public DateTimeOffset ApprovedUtc { get; set; }
}

/// <summary>
/// Persists which script contents the user has explicitly allowed to run. This is the enforcement
/// point behind the manifest's declared <c>capabilities</c>: a script only runs once the user has
/// approved its exact current content, and re-approves whenever that content changes.
/// </summary>
public sealed class TrustStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly ISettingsStore _store;
    private readonly string _key;
    private readonly Dictionary<string, TrustRecord> _records;

    /// <summary>
    /// Creates a trust store backed by <paramref name="store"/>, reading/writing the blob named
    /// <paramref name="key"/> (<c>trust.json</c> by default). This is the seam through which the trust
    /// store can later move into the protected settings store without changing any caller.
    /// </summary>
    public TrustStore(ISettingsStore store, string key = PowerScriptsPaths.TrustFileName)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _key = string.IsNullOrWhiteSpace(key) ? throw new ArgumentException("A blob key is required.", nameof(key)) : key;
        _records = Load(_store, _key);
    }

    /// <summary>
    /// Convenience constructor that persists to a specific file path (used by tests and any caller that
    /// wants an explicit location). Wraps a <see cref="FileSettingsStore"/> rooted at the file's
    /// directory so the on-disk behavior is identical to before this seam was introduced.
    /// </summary>
    public TrustStore(string path)
        : this(
            new FileSettingsStore(Path.GetDirectoryName(Path.GetFullPath(path ?? throw new ArgumentNullException(nameof(path))))!),
            Path.GetFileName(path))
    {
    }

    /// <summary>All current trust records.</summary>
    public IReadOnlyCollection<TrustRecord> Records => _records.Values;

    /// <summary>Returns true if the user has approved this id with exactly this content hash.</summary>
    public bool IsTrusted(string id, string hash)
    {
        if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(hash))
        {
            return false;
        }

        return _records.TryGetValue(id, out var record)
            && string.Equals(record.Hash, hash, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Records (or updates) approval for an id at the given content hash and persists it.</summary>
    public void Trust(TrustRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        _records[record.Id] = record;
        Save();
    }

    /// <summary>Removes approval for an id. Returns true if a record was removed.</summary>
    public bool Revoke(string id)
    {
        if (string.IsNullOrEmpty(id) || !_records.Remove(id))
        {
            return false;
        }

        Save();
        return true;
    }

    private static Dictionary<string, TrustRecord> Load(ISettingsStore store, string key)
    {
        var result = new Dictionary<string, TrustRecord>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var text = store.ReadBlob(key);
            if (!string.IsNullOrEmpty(text))
            {
                var records = JsonSerializer.Deserialize<List<TrustRecord>>(text, Options);
                if (records is not null)
                {
                    foreach (var record in records.Where(r => !string.IsNullOrEmpty(r.Id)))
                    {
                        result[record.Id] = record;
                    }
                }
            }
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            // A corrupt or unreadable trust store is treated as "nothing trusted" so the user is
            // simply re-prompted, rather than crashing every surface that runs a script.
        }

        return result;
    }

    private void Save() => _store.WriteBlob(_key, JsonSerializer.Serialize(_records.Values.ToList(), Options));
}
