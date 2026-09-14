// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text;
using PowerToys.TryRun.Core;

namespace PowerToys.TryRun.FuzzTests;

public static class WorkspaceFuzzer
{
    public static void FuzzTarget(ReadOnlySpan<byte> data)
    {
        if (data.Length > 16384)
        {
            return;
        }

        foreach (var name in new[] { "app.exe", "run", "run.sh", "script.py", "data.txt" })
        {
            var entry = EntryPointDetector.Detect(name, data);
            if (entry is not null && (!Enum.IsDefined(entry.Kind) || entry.RelativePath != name))
            {
                throw new InvalidOperationException("Invalid detected entry point.");
            }
        }

        try
        {
            TaskBundle.ParseLaunchArguments(Encoding.UTF8.GetString(data).Split('\n'));
        }
        catch (ArgumentException)
        {
            // Launch payloads contain only bounded absolute local paths.
        }

        var preview = WorkspaceSnapshot.FormatPreview(data, false);
        if (preview.Length > WorkspaceSnapshot.PreviewBytes + 100)
        {
            throw new InvalidOperationException("Unbounded workspace preview.");
        }

        try
        {
            var path = WorkspacePath.ValidateRelative(Encoding.UTF8.GetString(data));
            var resolved = Path.GetFullPath(Path.Combine("C:\\TryRunFuzz", path));
            if (!resolved.StartsWith("C:\\TryRunFuzz\\", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Workspace path escaped its root.");
            }

            var file = new WorkspaceFile(data.Length, "content-hash", preview);
            var snapshot = new Dictionary<string, WorkspaceFile> { [path] = file };
            if (WorkspaceSnapshot.Compare(snapshot, snapshot).Single().Kind != FileChangeKind.Unchanged)
            {
                throw new InvalidOperationException("Identical snapshots differ.");
            }
        }
        catch (ArgumentException)
        {
            // Invalid names and encodings are expected fuzz inputs.
        }
    }

    public static void FileRoundTrip(ReadOnlySpan<byte> data)
    {
        if (data.Length is < 2 or > 4096)
        {
            return;
        }

        var nameLength = Math.Min(data[0], data.Length - 1);
        string name;
        try
        {
            name = WorkspacePath.ValidateRelative(Encoding.UTF8.GetString(data.Slice(1, nameLength)));
        }
        catch (ArgumentException)
        {
            return;
        }

        using var source = new RunSession();
        using var session = new RunSession();
        using var destination = new RunSession();
        var path = Path.Combine(source.WorkingDirectory, name);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var bytes = data[(1 + nameLength)..].ToArray();
        File.WriteAllBytes(path, bytes);
        var workspace = new FileWorkspace(session);
        workspace.Import([path], CancellationToken.None);
        var changes = workspace.Review(CancellationToken.None);
        if (changes.Count != 1 || changes[0].Kind != FileChangeKind.Unchanged)
        {
            throw new InvalidOperationException("Import changed the input file.");
        }

        var output = workspace.Export(destination.WorkingDirectory, [Path.GetFileName(name)], CancellationToken.None);
        if (!File.ReadAllBytes(Path.Combine(output, Path.GetFileName(name))).AsSpan().SequenceEqual(bytes) || !File.ReadAllBytes(path).AsSpan().SequenceEqual(bytes))
        {
            throw new InvalidOperationException("File round trip changed content.");
        }
    }
}
