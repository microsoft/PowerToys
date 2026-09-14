// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace PowerToys.TryRun.Core;

public static class RunArtifacts
{
    public const string ObservationFileName = "tryrun-observations.json";

    public static string DiagnosticsDirectory(ExecutionRequest request)
    {
        RunSession.ValidateDirectories(request.WorkingDirectory, request.TemporaryDirectory);
        var path = Path.Combine(Path.GetDirectoryName(request.WorkingDirectory)!, "Diagnostics");
        Directory.CreateDirectory(path);
        using var directory = new WorkspaceFileSystem.DirectoryLease(path);
        if (!string.Equals(path, directory.Path, StringComparison.OrdinalIgnoreCase))
        {
            throw new IOException("The diagnostics folder is redirected.");
        }

        return directory.Path;
    }

    public static byte[] ReadFile(string path, string root, int maximumBytes)
    {
        path = WorkspacePath.LocalPath(path);
        root = WorkspacePath.LocalPath(root);
        if (!WorkspacePath.IsWithin(path, root) || path.Equals(root, StringComparison.OrdinalIgnoreCase))
        {
            throw new IOException("The report path is outside its owned directory.");
        }

        using var directory = new WorkspaceFileSystem.DirectoryLease(Path.GetDirectoryName(path)!);
        using var stream = WorkspaceFileSystem.OpenRead(Path.Combine(directory.Path, Path.GetFileName(path)));
        if (!WorkspacePath.IsWithin(PhysicalDirectory.Resolve(stream.SafeFileHandle), root))
        {
            throw new IOException("The report path is redirected outside its owned directory.");
        }

        if (stream.Length > maximumBytes)
        {
            throw new IOException("The report exceeds its size limit.");
        }

        var bytes = new byte[(int)stream.Length];
        stream.ReadExactly(bytes);
        return bytes;
    }
}
