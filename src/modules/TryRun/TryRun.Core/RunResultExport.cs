// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace PowerToys.TryRun.Core;

public static class RunResultExport
{
    public static string? Export(FileWorkspace workspace, IReadOnlyList<FileChange> changes, string? parent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentNullException.ThrowIfNull(changes);
        cancellationToken.ThrowIfCancellationRequested();
        if (changes.Count > WorkspacePath.MaximumEntries)
        {
            throw new ArgumentException("A result review may contain at most 1,000 entries.", nameof(changes));
        }

        var selected = changes.Where(change => change.After is not null).Select(change => change.RelativePath).ToArray();
        if (selected.Length == 0)
        {
            return null;
        }

        if (parent is null)
        {
            parent = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "PowerToys", "TryRun", "Results");
            CreateOwnedDirectory(parent, cancellationToken);
        }
        else
        {
            parent = WorkspacePath.LocalPath(parent);
            if (!Directory.Exists(parent))
            {
                throw new DirectoryNotFoundException("The results parent directory must already exist.");
            }
        }

        // Hold every ancestor against rename while the reviewed workspace is
        // exported. FileWorkspace creates a new folder and never merges output.
        using var lease = new WorkspaceFileSystem.DirectoryLease(parent);
        return workspace.Export(lease.Path, selected, cancellationToken);
    }

    private static void CreateOwnedDirectory(string path, CancellationToken cancellationToken)
    {
        path = WorkspacePath.LocalPath(path);
        for (var directory = new DirectoryInfo(path); directory is not null; directory = directory.Parent)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var attributes = File.GetAttributes(directory.FullName);
                if ((attributes & FileAttributes.ReparsePoint) != 0 || (attributes & FileAttributes.Directory) == 0)
                {
                    throw new IOException("Results directories cannot be links, junctions, or files.");
                }
            }
            catch (FileNotFoundException)
            {
                // Missing components are created only after all existing
                // ancestors have been checked.
            }
            catch (DirectoryNotFoundException)
            {
                // A missing ancestor is queued for safe creation below.
            }
        }

        var pending = new Stack<string>();
        var current = path;
        while (!Directory.Exists(current))
        {
            pending.Push(current);
            current = Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(current)) ?? throw new IOException("The results directory has no existing local parent.");
        }

        while (pending.TryPop(out var next))
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var lease = new WorkspaceFileSystem.DirectoryLease(current);
            Directory.CreateDirectory(next);
            using var created = new WorkspaceFileSystem.DirectoryLease(next);
            current = next;
        }
    }
}
