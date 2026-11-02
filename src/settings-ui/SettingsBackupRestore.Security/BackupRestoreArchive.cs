// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace Microsoft.PowerToys.SettingsBackupRestore.Security;

/// <summary>
/// Validates and extracts legacy .ptb ZIP archives through rooted handles.
/// </summary>
public static class BackupRestoreArchive
{
    private const int MaximumEntries = 10_000;
    internal const int MaximumPathNodes = 4_096;
    internal const int MaximumPathDepth = 64;
    internal const int MaximumPathLength = 1_024;
    private const long MaximumEntryLength = 64L * 1024 * 1024;
    private const long MaximumTotalLength = 256L * 1024 * 1024;

    /// <summary>
    /// Validates every archive name before extraction and returns normalized descriptors.
    /// </summary>
    public static IReadOnlyList<ArchiveEntryDescriptor> Validate(ZipArchive archive)
    {
        ArgumentNullException.ThrowIfNull(archive);
        if (archive.Entries.Count > MaximumEntries)
        {
            throw new InvalidDataException($"Archive contains more than {MaximumEntries} entries.");
        }

        List<ArchiveEntryDescriptor> descriptors = new(archive.Entries.Count);
        ArchivePathNode root = new();
        int pathNodeCount = 0;
        long totalLength = 0;

        foreach (ZipArchiveEntry entry in archive.Entries)
        {
            bool isDirectory = entry.FullName.EndsWith('/') || entry.FullName.EndsWith('\\');
            string rawPath = isDirectory ? entry.FullName.TrimEnd('/', '\\') : entry.FullName;
            string normalized = SecurePath.NormalizeRelative(rawPath);
            string[] components = normalized.Split('\\');
            if (normalized.Length > MaximumPathLength || components.Length > MaximumPathDepth)
            {
                throw new InvalidDataException($"Archive path exceeds restore limits: {entry.FullName}");
            }

            if (!isDirectory && entry.Length > MaximumEntryLength)
            {
                throw new InvalidDataException($"Archive entry is too large: {entry.FullName}");
            }

            totalLength = checked(totalLength + entry.Length);
            if (totalLength > MaximumTotalLength)
            {
                throw new InvalidDataException("Archive uncompressed size exceeds the restore limit.");
            }

            pathNodeCount = checked(pathNodeCount + root.Add(components, isDirectory, entry.FullName));
            if (pathNodeCount > MaximumPathNodes)
            {
                throw new InvalidDataException($"Archive paths exceed the restore complexity limit of {MaximumPathNodes} components.");
            }

            ArchiveEntryDescriptor descriptor = new(normalized, isDirectory, entry.Length);
            descriptors.Add(descriptor);
        }

        return descriptors;
    }

    /// <summary>
    /// Opens the archive relative to one root, validates all names, then extracts to a random exclusive directory under another root.
    /// </summary>
    public static SecureDirectoryRoot ExtractToExclusiveStaging(
        SecureDirectoryRoot archiveRoot,
        string archiveRelativePath,
        SecureDirectoryRoot stagingParent)
    {
        return ExtractToExclusiveStaging(archiveRoot, archiveRelativePath, stagingParent, beforeEntryCopy: null);
    }

    internal static SecureDirectoryRoot ExtractToExclusiveStaging(
        SecureDirectoryRoot archiveRoot,
        string archiveRelativePath,
        SecureDirectoryRoot stagingParent,
        Action<int>? beforeEntryCopy,
        Action<string>? beforeDirectoryCreate = null,
        Action<string>? afterCleanupStep = null,
        Action<SecureDirectoryRoot>? afterStagingCreated = null)
    {
        ArgumentNullException.ThrowIfNull(archiveRoot);
        ArgumentNullException.ThrowIfNull(stagingParent);

        using SecureFile archiveFile = archiveRoot.OpenFileForRead(archiveRelativePath);
        return ExtractToExclusiveStaging(archiveFile, stagingParent, beforeEntryCopy, beforeDirectoryCreate, afterCleanupStep, afterStagingCreated);
    }

    internal static SecureDirectoryRoot ExtractToExclusiveStaging(
        SecureFile archiveFile,
        SecureDirectoryRoot stagingParent,
        Action<int>? beforeEntryCopy = null,
        Action<string>? beforeDirectoryCreate = null,
        Action<string>? afterCleanupStep = null,
        Action<SecureDirectoryRoot>? afterStagingCreated = null)
    {
        ArgumentNullException.ThrowIfNull(archiveFile);
        ArgumentNullException.ThrowIfNull(stagingParent);
        archiveFile.Stream.Position = 0;
        using ZipArchive archive = new(archiveFile.Stream, ZipArchiveMode.Read, leaveOpen: true);
        IReadOnlyList<ArchiveEntryDescriptor> descriptors = Validate(archive);

        SecureDirectoryRoot? staging = null;
        List<string> createdFiles = [];
        SortedSet<string> createdDirectories = new(WindowsPathComparer.Instance);
        try
        {
            staging = stagingParent.CreateExclusiveStagingDirectory();
            afterStagingCreated?.Invoke(staging);
            SortedSet<string> requiredDirectories = new(WindowsPathComparer.Instance);
            foreach (ArchiveEntryDescriptor descriptor in descriptors)
            {
                if (descriptor.IsDirectory)
                {
                    AddDirectoryAndAncestors(requiredDirectories, descriptor.RelativePath);
                }
                else
                {
                    AddParentDirectories(requiredDirectories, descriptor.RelativePath);
                }
            }

            foreach (string directory in requiredDirectories
                         .OrderBy(path => path.Count(character => character == '\\'))
                         .ThenBy(path => path, WindowsPathComparer.Instance))
            {
                beforeDirectoryCreate?.Invoke(directory);
                staging.CreateNewDirectory(directory);
                createdDirectories.Add(directory);
            }

            for (int index = 0; index < descriptors.Count; index++)
            {
                ArchiveEntryDescriptor descriptor = descriptors[index];
                ZipArchiveEntry entry = archive.Entries[index];
                if (descriptor.IsDirectory)
                {
                    continue;
                }

                beforeEntryCopy?.Invoke(index);
                using SecureFile destination = staging.CreateNewFileInExistingDirectory(descriptor.RelativePath);
                createdFiles.Add(descriptor.RelativePath);
                using Stream source = entry.Open();
                destination.CopyFrom(source);
            }

            SecureDirectoryRoot result = staging;
            staging = null;
            return result;
        }
        catch (Exception extractionException)
        {
            if (staging != null)
            {
                List<Exception> cleanupErrors = [];
                try
                {
                    for (int index = createdFiles.Count - 1; index >= 0; index--)
                    {
                        TryCleanup(
                            () =>
                            {
                                staging.DeleteEntry(createdFiles[index], isDirectory: false);
                                afterCleanupStep?.Invoke(createdFiles[index]);
                            },
                            cleanupErrors);
                    }

                    foreach (string directory in createdDirectories.OrderByDescending(path => path.Length))
                    {
                        TryCleanup(
                            () =>
                            {
                                staging.DeleteEntry(directory, isDirectory: true);
                                afterCleanupStep?.Invoke(directory);
                            },
                            cleanupErrors);
                    }

                    TryCleanup(
                        () =>
                        {
                            staging.DeleteSelf();
                            afterCleanupStep?.Invoke(string.Empty);
                        },
                        cleanupErrors);
                }
                finally
                {
                    staging.Dispose();
                }

                if (cleanupErrors.Count > 0)
                {
                    extractionException.Data["StagingCleanupErrors"] = cleanupErrors.ToArray();
                }
            }

            throw;
        }
    }

    private static void AddDirectoryAndAncestors(ISet<string> directories, string directory)
    {
        directories.Add(directory);
        AddParentDirectories(directories, directory);
    }

    private static void AddParentDirectories(ISet<string> directories, string path)
    {
        int separatorIndex = path.IndexOf('\\');
        while (separatorIndex >= 0)
        {
            directories.Add(path[..separatorIndex]);
            separatorIndex = path.IndexOf('\\', separatorIndex + 1);
        }
    }

    private static void TryCleanup(Action cleanup, List<Exception> cleanupErrors)
    {
        try
        {
            cleanup();
        }
        catch (Exception exception)
        {
            cleanupErrors.Add(exception);
        }
    }

    private sealed class ArchivePathNode
    {
        private readonly SortedDictionary<string, ArchivePathNode> children = new(WindowsPathComparer.Instance);
        private bool? isDirectoryEntry;

        internal int Add(string[] components, bool isDirectory, string originalPath)
        {
            ArchivePathNode current = this;
            int addedNodes = 0;
            for (int index = 0; index < components.Length; index++)
            {
                if (!current.children.TryGetValue(components[index], out ArchivePathNode? child))
                {
                    child = new ArchivePathNode();
                    current.children.Add(components[index], child);
                    addedNodes++;
                }

                current = child;
                if (index < components.Length - 1 && current.isDirectoryEntry == false)
                {
                    throw new InvalidDataException($"Archive file conflicts with a child path: {string.Join('\\', components[..(index + 1)])}");
                }
            }

            if (current.isDirectoryEntry.HasValue)
            {
                throw new InvalidDataException($"Archive path collision after Windows normalization: {originalPath}");
            }

            if (!isDirectory && current.children.Count > 0)
            {
                throw new InvalidDataException($"Archive file conflicts with a child path: {string.Join('\\', components)}");
            }

            current.isDirectoryEntry = isDirectory;
            return addedNodes;
        }
    }
}
