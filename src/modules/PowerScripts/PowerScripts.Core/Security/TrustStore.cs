// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;
using System.Text.Json.Serialization;

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

    private readonly string _path;
    private readonly Dictionary<string, TrustRecord> _records;

    public TrustStore(string path)
    {
        _path = path ?? throw new ArgumentNullException(nameof(path));
        _records = Load(path);
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

    private static Dictionary<string, TrustRecord> Load(string path)
    {
        var result = new Dictionary<string, TrustRecord>(StringComparer.OrdinalIgnoreCase);
        try
        {
            if (File.Exists(path))
            {
                var records = JsonSerializer.Deserialize<List<TrustRecord>>(File.ReadAllText(path), Options);
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
            // A corrupt or unreadable trust file is treated as "nothing trusted" so the user is
            // simply re-prompted, rather than crashing every surface that runs a script.
        }

        return result;
    }

    private void Save()
    {
        var directory = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(_path, JsonSerializer.Serialize(_records.Values.ToList(), Options));
    }
}
