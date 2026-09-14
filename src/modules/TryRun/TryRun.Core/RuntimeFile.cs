// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace PowerToys.TryRun.Core;

public static class RuntimeFile
{
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
