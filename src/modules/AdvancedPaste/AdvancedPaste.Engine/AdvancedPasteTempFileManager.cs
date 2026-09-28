// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Linq;

using ManagedCommon;

namespace AdvancedPaste.Helpers;

internal static class AdvancedPasteTempFileManager
{
    private const string DirectoryPrefix = "PowerToys_AdvancedPaste_";

    internal static DirectoryInfo CreateDirectory()
        => Directory.CreateTempSubdirectory(DirectoryPrefix);

    internal static void CleanupStaleDirectories(TimeSpan maximumAge)
    {
        var cutoff = DateTime.UtcNow - maximumAge;
        var tempDirectory = new DirectoryInfo(Path.GetTempPath());

        foreach (var directory in tempDirectory.EnumerateDirectories($"{DirectoryPrefix}*"))
        {
            try
            {
                if (directory.CreationTimeUtc >= cutoff || directory.Attributes.HasFlag(FileAttributes.ReparsePoint))
                {
                    continue;
                }

                foreach (var file in directory.EnumerateFiles())
                {
                    file.Delete();
                }

                if (!directory.EnumerateFileSystemInfos().Any())
                {
                    directory.Delete();
                }
            }
            catch (Exception ex)
            {
                Logger.LogDebug($"Failed to clean stale Advanced Paste temporary directory: {ex.Message}");
            }
        }
    }
}
