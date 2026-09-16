// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace PowerToys.TryRun.Core;

public static class RuntimeFile
{
    public static string ResolveInstallationDirectory(string application)
    {
        // Resolve through the selected file, which Windows can allow even when
        // a protected ancestor (such as WindowsApps) cannot be opened directly.
        var expected = Path.GetDirectoryName(Resolve(application))!;
        using var directory = WorkspaceFileSystem.OpenDirectory(expected);
        var resolved = PhysicalDirectory.Resolve(directory);
        if (!string.Equals(expected, resolved, StringComparison.OrdinalIgnoreCase))
        {
            throw new IOException("The application folder was redirected while being inspected. Select the application again.");
        }

        return resolved;
    }

    public static string Resolve(string path)
    {
        path = WorkspacePath.LocalPath(path);
        if ((File.GetAttributes(path) & (FileAttributes.Directory | FileAttributes.ReparsePoint)) != 0)
        {
            throw new IOException("Choose a regular application or image file, rather than a link or directory.");
        }

        using var handle = File.OpenHandle(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        return PhysicalDirectory.Resolve(handle);
    }
}
