// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.UITests;

internal static class IndexedFixtureCleanup
{
    internal static void Remove(string filePath, string folderPath, string childFileName)
    {
        DeleteOwnedFile(filePath);
        if (Directory.Exists(folderPath))
        {
            ClearReadOnly(folderPath);
            DeleteOwnedFile(Path.Combine(folderPath, childFileName));

            // Delete only the known fixture, never recursively remove unexpected OneDrive content.
            Directory.Delete(folderPath);
        }
    }

    private static void DeleteOwnedFile(string path)
    {
        if (!string.IsNullOrEmpty(path) && File.Exists(path))
        {
            ClearReadOnly(path);
            File.Delete(path);
        }
    }

    private static void ClearReadOnly(string path)
    {
        var attributes = File.GetAttributes(path);
        if ((attributes & FileAttributes.ReadOnly) != 0)
        {
            File.SetAttributes(path, attributes & ~FileAttributes.ReadOnly);
        }
    }
}
