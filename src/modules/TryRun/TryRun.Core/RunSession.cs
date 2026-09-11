// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace PowerToys.TryRun.Core;

public sealed class RunSession : IDisposable
{
    private static readonly string SessionsDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "PowerToys", "TryRun", "Sessions");
    private readonly string root;
    private bool disposed;

    public RunSession()
    {
        root = Path.Combine(SessionsDirectory, Guid.NewGuid().ToString("N"));
        WorkingDirectory = Path.Combine(root, "Work");
        TemporaryDirectory = Path.Combine(root, "Temp");
        Directory.CreateDirectory(WorkingDirectory);
        Directory.CreateDirectory(TemporaryDirectory);
        ValidateDirectories(WorkingDirectory, TemporaryDirectory);
    }

    public string WorkingDirectory { get; }

    public string TemporaryDirectory { get; }

    public static void ValidateDirectories(string workingDirectory, string temporaryDirectory)
    {
        var working = Path.TrimEndingDirectorySeparator(Path.GetFullPath(workingDirectory));
        var temporary = Path.TrimEndingDirectorySeparator(Path.GetFullPath(temporaryDirectory));
        var sessionRoot = Path.GetDirectoryName(working)!;
        if (!Guid.TryParseExact(Path.GetFileName(sessionRoot), "N", out _) ||
            !string.Equals(Path.GetDirectoryName(sessionRoot), SessionsDirectory, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(working, Path.Combine(sessionRoot, "Work"), StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(temporary, Path.Combine(sessionRoot, "Temp"), StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Execution is allowed only in a Try Run session workspace.");
        }

        foreach (var path in new[] { working, temporary })
        {
            for (var directory = new DirectoryInfo(path); directory is not null; directory = directory.Parent)
            {
                if (!directory.Exists || (directory.Attributes & FileAttributes.ReparsePoint) != 0)
                {
                    throw new IOException("Session directories must exist and cannot contain directory links.");
                }
            }
        }
    }

    public void Dispose()
    {
        if (!disposed)
        {
            DeleteDirectory(root);
            disposed = true;
        }
    }

    private static void DeleteDirectory(string path)
    {
        if (!Directory.Exists(path))
        {
            return;
        }

        var pending = new Stack<(string Path, bool Visited)>();
        pending.Push((path, false));
        while (pending.TryPop(out var current))
        {
            // Never traverse a directory link created by a workload. Iterate
            // instead of recursing so deeply nested output cannot exhaust stack.
            if (current.Visited || (File.GetAttributes(current.Path) & FileAttributes.ReparsePoint) != 0)
            {
                Directory.Delete(current.Path);
                continue;
            }

            pending.Push((current.Path, true));
            foreach (var entry in Directory.EnumerateFileSystemEntries(current.Path))
            {
                if ((File.GetAttributes(entry) & FileAttributes.Directory) != 0)
                {
                    pending.Push((entry, false));
                }
                else
                {
                    File.Delete(entry);
                }
            }
        }
    }
}
