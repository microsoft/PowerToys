// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace PowerToys.TryRun.Core;

public sealed class TaskBundle
{
    private TaskBundle(string[] inputs, List<TaskEntryPoint> entryPoints, int entryCount, long bytes)
    {
        Inputs = Array.AsReadOnly(inputs);
        EntryPoints = entryPoints.OrderBy(entry => entry.RelativePath, StringComparer.OrdinalIgnoreCase).ToList().AsReadOnly();
        EntryCount = entryCount;
        Bytes = bytes;
    }

    public IReadOnlyList<string> Inputs { get; }

    public IReadOnlyList<TaskEntryPoint> EntryPoints { get; }

    public int EntryCount { get; }

    public long Bytes { get; }

    public static string[] ParseLaunchArguments(IEnumerable<string> arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        var paths = arguments.Take(WorkspacePath.MaximumEntries + 2).ToArray();
        if (paths.FirstOrDefault() == "--")
        {
            paths = paths[1..];
        }

        if (paths.Length > WorkspacePath.MaximumEntries || paths.Sum(path => (long)(path?.Length ?? 0)) > 32768)
        {
            throw new ArgumentException("Choose at most 1,000 paths. Use a folder for a larger selection.");
        }

        return paths.Select(WorkspacePath.LocalPath).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public static TaskBundle Inspect(IEnumerable<string> paths, CancellationToken cancellationToken)
    {
        var selected = ParseLaunchArguments(paths);

        // A selected parent already includes its children. Retain only that
        // explicitly selected parent; never import unselected sibling files.
        var roots = selected.Where(path => !selected.Any(parent => !path.Equals(parent, StringComparison.OrdinalIgnoreCase) && WorkspacePath.IsWithin(path, parent))).ToArray();
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var entries = new List<TaskEntryPoint>();
        var entryCount = 0;
        long bytes = 0;
        foreach (var root in roots)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var name = WorkspacePath.ValidateRelative(Path.GetFileName(root));
            if (!names.Add(name))
            {
                throw new IOException($"Two selections have the same name: {name}. Select a containing folder or rename one.");
            }

            using var parent = new WorkspaceFileSystem.DirectoryLease(Path.GetDirectoryName(root)!);
            Visit(root, name);
        }

        return new TaskBundle(roots, entries, entryCount, bytes);

        void Visit(string source, string relative)
        {
            cancellationToken.ThrowIfCancellationRequested();
            relative = WorkspacePath.ValidateRelative(relative);
            if (++entryCount > WorkspacePath.MaximumEntries)
            {
                throw new IOException("Select at most 1,000 files and folders in total.");
            }

            var attributes = File.GetAttributes(source);
            if ((attributes & FileAttributes.ReparsePoint) != 0)
            {
                throw new IOException($"Links and junctions are not supported: {relative}");
            }

            if ((attributes & FileAttributes.Directory) != 0)
            {
                using var directory = new WorkspaceFileSystem.DirectoryLease(source);
                foreach (var child in Directory.EnumerateFileSystemEntries(directory.Path))
                {
                    Visit(child, Path.Combine(relative, Path.GetFileName(child)));
                }
            }
            else
            {
                using var stream = WorkspaceFileSystem.OpenRead(source);
                bytes += stream.Length;
                if (stream.Length > WorkspacePath.MaximumFileBytes || bytes > WorkspacePath.MaximumTotalBytes)
                {
                    throw new IOException("Files must be at most 32 MiB each and 100 MiB in total.");
                }

                var header = new byte[(int)Math.Min(stream.Length, EntryPointDetector.HeaderBytes)];
                stream.ReadExactly(header);
                if (EntryPointDetector.Detect(relative, header) is { } entry)
                {
                    entries.Add(entry);
                }
            }
        }
    }

    public string GetRelativePath(string sourcePath)
    {
        sourcePath = WorkspacePath.LocalPath(sourcePath);
        foreach (var root in Inputs)
        {
            if (WorkspacePath.IsWithin(sourcePath, root))
            {
                return WorkspacePath.ValidateRelative(root.Equals(sourcePath, StringComparison.OrdinalIgnoreCase) ? Path.GetFileName(root) : Path.Combine(Path.GetFileName(root), Path.GetRelativePath(root, sourcePath)));
            }
        }

        throw new ArgumentException("The entry point must be part of the selected files.");
    }
}
