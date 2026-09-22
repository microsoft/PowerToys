// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace PowerToys.TryRun.Core;

// Profiles are same-user configuration, not an authorization boundary. The
// revision detects changed configuration; it is not a signature or a trust grant.
public sealed class PolicyProfileStore
{
    public const int MaximumProfiles = 256;

    private readonly string directory;

    // AppData can be virtualized separately for each MSIX package. Profiles
    // must be shared by packaged CmdPal and ordinary desktop/CLI callers.
    public static string DefaultDirectory => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".powertoys", "TryRun", "Policies");

    public PolicyProfileStore(string? directory = null)
    {
        this.directory = WorkspacePath.LocalPath(directory ?? DefaultDirectory);
    }

    public IReadOnlyList<PolicyProfile> List()
    {
        RejectExistingLinks(directory);
        if (!Directory.Exists(directory))
        {
            return [];
        }

        using var lease = new WorkspaceFileSystem.DirectoryLease(directory);
        var files = Directory.EnumerateFileSystemEntries(lease.Path, "*.json", SearchOption.TopDirectoryOnly).Take(MaximumProfiles + 1).ToArray();
        if (files.Length > MaximumProfiles)
        {
            throw new InvalidDataException($"The policy store contains more than {MaximumProfiles} profiles.");
        }

        return files.Select(path => Read(path, Path.GetFileNameWithoutExtension(path), null))
            .OrderBy(profile => profile.Name, StringComparer.OrdinalIgnoreCase).ThenBy(profile => profile.Id, StringComparer.Ordinal).ToArray();
    }

    public PolicyProfile Load(string id, string? expectedRevision = null)
    {
        PolicyProfileCodec.ValidateId(id);
        if (expectedRevision is not null)
        {
            PolicyProfileCodec.ValidateRevision(expectedRevision);
        }

        using var lease = new WorkspaceFileSystem.DirectoryLease(directory);
        return Read(Path.Combine(lease.Path, id + ".json"), id, expectedRevision);
    }

    public PolicyProfile Save(string name, bool linux, PolicySettings policy, string? id = null)
    {
        var profile = PolicyProfileCodec.Create(name, linux, policy, id);
        var bytes = PolicyProfileCodec.Serialize(profile);
        CreateDirectory(directory);
        using var lease = new WorkspaceFileSystem.DirectoryLease(directory);
        var destination = Path.Combine(lease.Path, profile.Id + ".json");
        ValidateDestination(destination);
        if (!File.Exists(destination) && Directory.EnumerateFileSystemEntries(lease.Path, "*.json", SearchOption.TopDirectoryOnly).Take(MaximumProfiles).Count() == MaximumProfiles)
        {
            throw new InvalidDataException($"Save at most {MaximumProfiles} policy profiles.");
        }

        var temporary = Path.Combine(lease.Path, profile.Id + "." + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                stream.Write(bytes);
                stream.Flush(flushToDisk: true);
            }

            // Replace only after the complete validated snapshot is durable.
            // The temporary file is on the same volume as the destination.
            ValidateDestination(destination);
            File.Move(temporary, destination, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }
        }

        return profile;
    }

    private static PolicyProfile Read(string path, string id, string? expectedRevision)
    {
        PolicyProfileCodec.ValidateId(id);
        using var stream = WorkspaceFileSystem.OpenRead(path);
        if (stream.Length > PolicyProfileCodec.MaximumDocumentBytes)
        {
            throw new InvalidDataException("The policy profile is too large.");
        }

        var bytes = new byte[checked((int)stream.Length)];
        stream.ReadExactly(bytes);
        var profile = PolicyProfileCodec.Parse(bytes);
        if (profile.Id != id)
        {
            throw new InvalidDataException("The profile ID does not match its file name.");
        }

        if (expectedRevision is not null && profile.Revision != expectedRevision)
        {
            throw new InvalidDataException("The policy profile changed after it was selected. Review the current revision before running.");
        }

        return profile;
    }

    private static void ValidateDestination(string path)
    {
        try
        {
            _ = File.GetAttributes(path);
        }
        catch (FileNotFoundException)
        {
            return;
        }

        using var stream = WorkspaceFileSystem.OpenRead(path);
    }

    private static void CreateDirectory(string path)
    {
        RejectExistingLinks(path);
        var pending = new Stack<string>();
        var current = path;
        while (!Directory.Exists(current))
        {
            pending.Push(current);
            current = Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(current)) ?? throw new IOException("The policy directory has no existing local parent.");
        }

        while (pending.TryPop(out var next))
        {
            using var lease = new WorkspaceFileSystem.DirectoryLease(current);
            Directory.CreateDirectory(next);
            using var created = new WorkspaceFileSystem.DirectoryLease(next);
            current = next;
        }
    }

    private static void RejectExistingLinks(string path)
    {
        for (var current = new DirectoryInfo(path); current is not null; current = current.Parent)
        {
            try
            {
                var attributes = File.GetAttributes(current.FullName);
                if ((attributes & FileAttributes.ReparsePoint) != 0 || (attributes & FileAttributes.Directory) == 0)
                {
                    throw new IOException("Policy store directories cannot be links, junctions, or files.");
                }
            }
            catch (FileNotFoundException)
            {
                // Save creates missing directories only after inspecting parents.
            }
            catch (DirectoryNotFoundException)
            {
                // List does not create an empty profile store.
            }
        }
    }
}
