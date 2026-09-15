// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace PowerToys.TryRun.Core;

public static class PolicyPaths
{
    public static string Normalize(string path) => WorkspacePath.LocalPath(path);

    public static bool IsWithin(string path, string parent) => WorkspacePath.IsWithin(path, parent);

    public static string ResolveExisting(string path)
    {
        path = WorkspacePath.LocalPath(path);
        using var parent = new WorkspaceFileSystem.DirectoryLease(Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(path)) ?? path);
        if (Directory.Exists(path))
        {
            using var directory = new WorkspaceFileSystem.DirectoryLease(path);
            return directory.Path;
        }

        return RuntimeFile.Resolve(path);
    }
}
