// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PowerToys.TryRun.Core;

public static class PolicyProfileCodec
{
    public const int MaximumDocumentBytes = 512 * 1024;
    public const int MaximumNameLength = 80;

    private static readonly string[] RequiredProperties = ["SchemaVersion", "Id", "Name", "Linux", "Revision", "Policy"];
    private static readonly string[] PathListFields = ["readonlyPaths", "readwritePaths", "deniedPaths"];
    private static readonly string[] SinglePathFields = ["storagePath", "captureOutputPath"];

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        MaxDepth = 16,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    private static readonly JsonDocumentOptions DocumentOptions = new() { MaxDepth = 16 };
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    public static PolicyProfile Parse(ReadOnlySpan<byte> data)
    {
        if (data.Length > MaximumDocumentBytes)
        {
            throw new InvalidDataException("The policy profile is too large.");
        }

        try
        {
            // JsonDocument can defer transcoding property names until they are
            // inspected. Reject invalid bytes before that deferred operation.
            _ = StrictUtf8.GetCharCount(data);
        }
        catch (DecoderFallbackException exception)
        {
            throw new InvalidDataException("The policy profile must contain valid UTF-8 text.", exception);
        }

        using var document = JsonDocument.Parse(data.ToArray(), DocumentOptions);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException("A policy profile must be a JSON object.");
        }

        RejectDuplicateProperties(root);
        foreach (var name in RequiredProperties)
        {
            if (!root.TryGetProperty(name, out _))
            {
                throw new InvalidDataException($"The policy profile is missing {name}.");
            }
        }

        var profile = root.Deserialize<PolicyProfile>(JsonOptions) ?? throw new InvalidDataException("Missing policy profile.");
        return Validate(profile);
    }

    public static byte[] Serialize(PolicyProfile profile)
    {
        profile = Validate(profile);
        return Write(profile, includeRevision: true);
    }

    internal static PolicyProfile Create(string name, bool linux, PolicySettings policy, string? id)
    {
        ArgumentNullException.ThrowIfNull(name);
        var profile = new PolicyProfile
        {
            Id = id ?? Guid.NewGuid().ToString("N"),
            Name = name.Trim(),
            Linux = linux,
            Policy = Materialize(policy, linux),
        };
        ValidateIdentity(profile);
        return profile with { Revision = ComputeRevision(profile) };
    }

    internal static void ValidateId(string id)
    {
        if (id is null || !Guid.TryParseExact(id, "N", out var parsed) || id != parsed.ToString("N"))
        {
            throw new ArgumentException("A policy profile ID must be a lowercase GUID without separators.");
        }
    }

    internal static void ValidateRevision(string revision)
    {
        if (revision is null || revision.Length != 64 || revision.Any(character => !char.IsAsciiDigit(character) && character is not (>= 'a' and <= 'f')))
        {
            throw new ArgumentException("A policy profile revision must be a lowercase SHA-256 value.");
        }
    }

    private static PolicyProfile Validate(PolicyProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ValidateIdentity(profile);
        ValidateRevision(profile.Revision);
        var snapshot = profile with { Policy = Materialize(profile.Policy, profile.Linux) };
        if (!string.Equals(snapshot.Revision, ComputeRevision(snapshot), StringComparison.Ordinal))
        {
            throw new InvalidDataException("The policy profile content does not match its revision. Save and review the profile again.");
        }

        return snapshot;
    }

    private static void ValidateIdentity(PolicyProfile profile)
    {
        if (profile.SchemaVersion != 1)
        {
            throw new InvalidDataException("Unsupported policy profile schema version.");
        }

        ValidateId(profile.Id);
        if (string.IsNullOrWhiteSpace(profile.Name) || profile.Name != profile.Name.Trim() || profile.Name.Length > MaximumNameLength ||
            profile.Name.Any(character => char.IsControl(character) || char.GetUnicodeCategory(character) == UnicodeCategory.Format))
        {
            throw new ArgumentException($"Choose a policy profile name of 1 to {MaximumNameLength} visible characters, without surrounding whitespace.");
        }

        _ = StrictUtf8.GetByteCount(profile.Name);
    }

    private static PolicySettings Materialize(PolicySettings policy, bool linux)
    {
        ArgumentNullException.ThrowIfNull(policy);
        policy.Validate(linux);
        var snapshot = PolicySettings.Defaults(linux);
        foreach (var field in PolicySettings.Fields)
        {
            var value = policy.Get(field.Key, linux);
            if (field.Kind is "lines" or "paths")
            {
                value = string.Join('\n', policy.Lines(field.Key, linux));
            }
            else if (field.Kind == "number")
            {
                value = uint.Parse(value, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture);
            }
            else if (field.Kind == "rules")
            {
                using var document = JsonDocument.Parse(value, DocumentOptions);
                RejectDuplicateProperties(document.RootElement);
                value = JsonSerializer.Serialize(policy.Rules(field.Key));
            }

            snapshot.Values[field.Key] = value;
        }

        snapshot.Validate(linux);
        RejectSessionPaths(snapshot, linux);
        return snapshot;
    }

    private static void RejectSessionPaths(PolicySettings policy, bool linux)
    {
        var sessions = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "PowerToys", "TryRun", "Sessions");
        var roots = new List<string> { sessions };
        if (Directory.Exists(sessions))
        {
            using var lease = new WorkspaceFileSystem.DirectoryLease(sessions);
            roots.Add(lease.Path);
        }

        var paths = PathListFields.SelectMany(key => policy.Lines(key, linux))
            .Concat(SinglePathFields.Select(key => policy.Get(key)));
        foreach (var path in paths.Where(path => path.Length > 0 && !path.StartsWith('$')))
        {
            var normalized = WorkspacePath.LocalPath(path);
            if (roots.Any(root => WorkspacePath.IsWithin(normalized, root)) || HasSessionNamespace(normalized))
            {
                throw new ArgumentException("Reusable profiles cannot refer to temporary Try Run session paths. Use $work, $temp, or $diagnostics instead.");
            }
        }
    }

    private static bool HasSessionNamespace(string path)
    {
        // A merged MSIX view can resolve an existing Sessions parent and a
        // newly created session into different physical roots. Retain the
        // session namespace check even after that session has been discarded.
        const string marker = "\\Microsoft\\PowerToys\\TryRun\\Sessions";
        var start = 0;
        while ((start = path.IndexOf(marker, start, StringComparison.OrdinalIgnoreCase)) >= 0)
        {
            var end = start + marker.Length;
            if (end == path.Length || path[end] == '\\')
            {
                return true;
            }

            start = end;
        }

        return false;
    }

    private static string ComputeRevision(PolicyProfile profile) => Convert.ToHexStringLower(SHA256.HashData(Write(profile, includeRevision: false)));

    private static byte[] Write(PolicyProfile profile, bool includeRevision)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteNumber("SchemaVersion", profile.SchemaVersion);
            writer.WriteString("Id", profile.Id);
            writer.WriteString("Name", profile.Name);
            writer.WriteBoolean("Linux", profile.Linux);
            if (includeRevision)
            {
                writer.WriteString("Revision", profile.Revision);
            }

            writer.WriteStartObject("Policy");
            writer.WriteStartObject("Values");
            foreach (var pair in profile.Policy.Values.OrderBy(pair => pair.Key, StringComparer.Ordinal))
            {
                writer.WriteString(pair.Key, pair.Value);
            }

            writer.WriteEndObject();
            writer.WriteEndObject();
            writer.WriteEndObject();
        }

        if (stream.Length > MaximumDocumentBytes)
        {
            throw new InvalidDataException("The policy profile is too large.");
        }

        return stream.ToArray();
    }

    private static void RejectDuplicateProperties(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in value.EnumerateObject())
            {
                if (!names.Add(property.Name))
                {
                    throw new InvalidDataException("Duplicate policy profile properties are not supported.");
                }

                RejectDuplicateProperties(property.Value);
            }
        }
        else if (value.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in value.EnumerateArray())
            {
                RejectDuplicateProperties(item);
            }
        }
    }
}
