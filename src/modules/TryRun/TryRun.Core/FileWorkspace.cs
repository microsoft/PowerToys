// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Security.Cryptography;

namespace PowerToys.TryRun.Core;

public sealed class FileWorkspace(RunSession session)
{
    private readonly RunSession session = session ?? throw new ArgumentNullException(nameof(session));
    private IReadOnlyDictionary<string, WorkspaceFile> baseline = new Dictionary<string, WorkspaceFile>();
    private IReadOnlyDictionary<string, WorkspaceFile>? reviewed;
    private bool imported;

    public void Import(IEnumerable<string> inputs, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        if (imported)
        {
            throw new InvalidOperationException("Each run needs a fresh workspace.");
        }

        ValidateSession();
        using var destination = new WorkspaceFileSystem.DirectoryLease(session.WorkingDirectory);
        if (Directory.EnumerateFileSystemEntries(destination.Path).Any())
        {
            throw new IOException("Import requires an empty workspace.");
        }

        var files = new Dictionary<string, WorkspaceFile>(StringComparer.OrdinalIgnoreCase);
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var budget = new Budget();
        foreach (var input in inputs)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var path = WorkspacePath.LocalPath(input);
            var name = WorkspacePath.ValidateRelative(Path.GetFileName(path));
            if (!names.Add(name))
            {
                throw new IOException($"Two inputs have the same name: {name}. Rename one or select a containing folder.");
            }

            using var source = new WorkspaceFileSystem.DirectoryLease(Path.GetDirectoryName(path)!);
            Visit(path, name, destination.Path, files, budget, cancellationToken);
        }

        baseline = files;
        imported = true;
    }

    public IReadOnlyList<FileChange> Review(CancellationToken cancellationToken)
    {
        reviewed = null;
        if (!imported)
        {
            throw new InvalidOperationException("Import must complete before reviewing a run.");
        }

        reviewed = Scan(cancellationToken);
        return WorkspaceSnapshot.Compare(baseline, reviewed);
    }

    public string Export(string parentDirectory, IEnumerable<string> selectedPaths, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(selectedPaths);
        var snapshot = reviewed ?? throw new InvalidOperationException("Review the run before exporting files.");
        var requested = selectedPaths.Take(WorkspacePath.MaximumEntries + 1).ToArray();
        if (requested.Length is 0 or > WorkspacePath.MaximumEntries)
        {
            throw new ArgumentException("Select between 1 and 1,000 result files.");
        }

        var selected = requested.Select(WorkspacePath.ValidateRelative).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (selected.Any(path => !snapshot.ContainsKey(path)))
        {
            throw new ArgumentException("Select existing result files to export. Deleted files cannot be exported.");
        }

        var current = Scan(cancellationToken);
        if (WorkspaceSnapshot.Compare(snapshot, current).Any(change => change.Kind != FileChangeKind.Unchanged))
        {
            throw new IOException("Workspace files changed after review. Review them again before exporting.");
        }

        using var parent = new WorkspaceFileSystem.DirectoryLease(parentDirectory);
        if (WorkspacePath.IsWithin(parent.Path, Path.GetDirectoryName(session.WorkingDirectory)!))
        {
            throw new IOException("Choose an export location outside this temporary session.");
        }

        cancellationToken.ThrowIfCancellationRequested();
        var result = WorkspaceFileSystem.CreateDirectory(Path.Combine(parent.Path, $"TryRun results {DateTime.Now:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}"));
        var directories = new Dictionary<string, WorkspaceFileSystem.DirectoryLease>(StringComparer.OrdinalIgnoreCase);
        try
        {
            directories.Add(string.Empty, new WorkspaceFileSystem.DirectoryLease(result));
            foreach (var relative in selected)
            {
                cancellationToken.ThrowIfCancellationRequested();
                using var sourceDirectory = new WorkspaceFileSystem.DirectoryLease(Path.GetDirectoryName(Path.Combine(session.WorkingDirectory, relative))!);
                using var source = WorkspaceFileSystem.OpenRead(Path.Combine(sourceDirectory.Path, Path.GetFileName(relative)));
                var actual = Describe(source, cancellationToken);
                if (actual.Hash != snapshot[relative].Hash || actual.Size != snapshot[relative].Size)
                {
                    throw new IOException("A selected file changed after review.");
                }

                var targetParent = string.Empty;
                foreach (var component in (Path.GetDirectoryName(relative) ?? string.Empty).Split('\\', StringSplitOptions.RemoveEmptyEntries))
                {
                    targetParent = Path.Combine(targetParent, component);
                    if (!directories.ContainsKey(targetParent))
                    {
                        var created = WorkspaceFileSystem.CreateDirectory(Path.Combine(result, targetParent));
                        directories.Add(targetParent, new WorkspaceFileSystem.DirectoryLease(created));
                    }
                }

                using var target = new FileStream(Path.Combine(directories[targetParent].Path, Path.GetFileName(relative)), FileMode.CreateNew, FileAccess.Write, FileShare.None);
                Copy(source, target, cancellationToken);
            }

            return result;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or OperationCanceledException)
        {
            // Leave only the new output directory for inspection. Never clean
            // up by recursively deleting a user-selected destination tree.
            throw new IOException($"Export did not finish. Partial copies may remain in: {result}", exception);
        }
        finally
        {
            foreach (var directory in directories.Values.Reverse())
            {
                directory.Dispose();
            }
        }
    }

    private Dictionary<string, WorkspaceFile> Scan(CancellationToken cancellationToken)
    {
        ValidateSession();
        using var root = new WorkspaceFileSystem.DirectoryLease(session.WorkingDirectory);
        var files = new Dictionary<string, WorkspaceFile>(StringComparer.OrdinalIgnoreCase);
        var budget = new Budget();
        foreach (var entry in Directory.EnumerateFileSystemEntries(root.Path))
        {
            Visit(entry, Path.GetFileName(entry), destination: null, files, budget, cancellationToken);
        }

        return files;
    }

    private void ValidateSession()
    {
        RunSession.ValidateDirectories(session.WorkingDirectory, session.TemporaryDirectory);
    }

    private static void Visit(string source, string relative, string? destination, Dictionary<string, WorkspaceFile> files, Budget budget, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        relative = WorkspacePath.ValidateRelative(relative);
        budget.AddEntry();
        var attributes = File.GetAttributes(source);
        if ((attributes & FileAttributes.ReparsePoint) != 0)
        {
            throw new IOException($"Links and junctions are not supported: {relative}");
        }

        if ((attributes & FileAttributes.Directory) != 0)
        {
            using var directory = WorkspaceFileSystem.OpenDirectory(source);
            using var physicalDirectory = new WorkspaceFileSystem.DirectoryLease(PhysicalDirectory.Resolve(directory));
            using var targetDirectory = destination is null ? null : new WorkspaceFileSystem.DirectoryLease(WorkspaceFileSystem.CreateDirectory(Path.Combine(destination, relative)));

            foreach (var child in Directory.EnumerateFileSystemEntries(physicalDirectory.Path))
            {
                Visit(child, Path.Combine(relative, Path.GetFileName(child)), destination, files, budget, cancellationToken);
            }
        }
        else
        {
            using var stream = WorkspaceFileSystem.OpenRead(source);
            budget.AddBytes(stream.Length);
            var description = Describe(stream, cancellationToken);
            files.Add(relative, description);
            if (destination is not null)
            {
                using var target = new FileStream(Path.Combine(destination, relative), FileMode.CreateNew, FileAccess.Write, FileShare.None);
                Copy(stream, target, cancellationToken);
            }
        }
    }

    private static WorkspaceFile Describe(FileStream stream, CancellationToken cancellationToken)
    {
        if (stream.Length > WorkspacePath.MaximumFileBytes)
        {
            throw new IOException("A file exceeds the 32 MiB limit.");
        }

        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = new byte[81920];
        int count;
        while ((count = stream.Read(buffer)) != 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            hash.AppendData(buffer, 0, count);
        }

        stream.Position = 0;
        var preview = new byte[(int)Math.Min(WorkspaceSnapshot.PreviewBytes, stream.Length)];
        stream.ReadExactly(preview);
        stream.Position = 0;
        return new WorkspaceFile(stream.Length, Convert.ToHexString(hash.GetHashAndReset()), WorkspaceSnapshot.FormatPreview(preview, stream.Length > preview.Length));
    }

    private static void Copy(FileStream source, FileStream target, CancellationToken cancellationToken)
    {
        var buffer = new byte[81920];
        int count;
        while ((count = source.Read(buffer)) != 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            target.Write(buffer, 0, count);
        }
    }

    private sealed class Budget
    {
        private int entries;
        private long bytes;

        public void AddEntry()
        {
            if (++entries > WorkspacePath.MaximumEntries)
            {
                throw new IOException("A workspace may contain at most 1,000 files and folders.");
            }
        }

        public void AddBytes(long size)
        {
            bytes += size;
            if (size > WorkspacePath.MaximumFileBytes || bytes > WorkspacePath.MaximumTotalBytes)
            {
                throw new IOException("Files must be at most 32 MiB each and 100 MiB in total.");
            }
        }
    }
}
