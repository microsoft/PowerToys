// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.Threading;
using ManagedCommon;
using Microsoft.CmdPal.Ext.Apps.Utils;

namespace Microsoft.CmdPal.Ext.Apps.Win32;

/// <summary>Enumerates supported files with bounded depth, cancellation and per-directory failure reporting.</summary>
internal static class Win32FileEnumerator
{
    private static readonly IFileSystem FileSystem = new FileSystem();

    internal static IFile FileWrapper { get; set; } = FileSystem.File;

    internal static IDirectory DirectoryWrapper { get; set; } = FileSystem.Directory;

    /// <summary>Enumerates supported files within the depth limit while reporting unreadable folders and continuing with siblings.</summary>
    /// <param name="directory">The root directory to scan.</param>
    /// <param name="suffixes">The accepted file suffixes.</param>
    /// <param name="maximumDepth">The greatest depth below the root; zero scans only the root.</param>
    /// <param name="onError">Receives unreadable paths so the caller can retain unconfirmed catalog data.</param>
    /// <param name="excludeDirectory">Optional predicate that prevents scanning excluded subtrees.</param>
    /// <param name="cancellationToken">Cancels enumeration between filesystem operations.</param>
    internal static IEnumerable<string> EnumerateFiles(
        string directory,
        IList<string> suffixes,
        int maximumDepth = int.MaxValue,
        Action<string, Exception>? onError = null,
        Func<string, bool>? excludeDirectory = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maximumDepth);

        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            if ((FileWrapper.GetAttributes(directory) & FileAttributes.Directory) == 0 || excludeDirectory?.Invoke(directory) == true)
            {
                return [];
            }
        }
        catch (Exception ex) when (PathHelpers.IsMissingOrInvalidPath(ex))
        {
            return [];
        }
        catch (Exception ex)
        {
            onError?.Invoke(directory, ex);
            Logger.LogError(ex.Message);
            return [];
        }

        var files = new List<string>();
        var folderQueue = new Queue<(string Path, int Depth)>();
        folderQueue.Enqueue((directory, 0));

        // Keep track of already visited directories to avoid cycles.
        var alreadyVisited = new HashSet<string>();

        do
        {
            cancellationToken.ThrowIfCancellationRequested();
            var current = folderQueue.Dequeue();
            var currentDirectory = current.Path;

            if (alreadyVisited.Contains(currentDirectory))
            {
                continue;
            }

            alreadyVisited.Add(currentDirectory);

            try
            {
                foreach (var suffix in suffixes)
                {
                    foreach (var file in DirectoryWrapper.EnumerateFiles(currentDirectory, $"*.{suffix}", SearchOption.TopDirectoryOnly))
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        files.Add(file);
                    }
                }
            }
            catch (Exception ex) when (PathHelpers.IsMissingOrInvalidPath(ex))
            {
                // A directory deleted during enumeration is a confirmed absence.
                continue;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                onError?.Invoke(currentDirectory, ex);
                Logger.LogError(ex.Message);
            }

            try
            {
                if (current.Depth >= maximumDepth)
                {
                    continue;
                }

                foreach (var childDirectory in DirectoryWrapper.EnumerateDirectories(currentDirectory, "*", new EnumerationOptions()
                {
                    // https://learn.microsoft.com/dotnet/api/system.io.enumerationoptions?view=net-6.0
                    // Exclude directories with the Reparse Point file attribute, to avoid loops due to symbolic links / directory junction / mount points.
                    AttributesToSkip = FileAttributes.Hidden | FileAttributes.System | FileAttributes.ReparsePoint,
                    RecurseSubdirectories = false,
                    IgnoreInaccessible = false,
                }))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (excludeDirectory?.Invoke(childDirectory) != true)
                    {
                        folderQueue.Enqueue((childDirectory, current.Depth + 1));
                    }
                }
            }
            catch (Exception ex) when (PathHelpers.IsMissingOrInvalidPath(ex))
            {
                // Continue with siblings that were already discovered.
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                onError?.Invoke(currentDirectory, ex);
                Logger.LogError(ex.Message);
            }
        }
        while (folderQueue.Count > 0);

        return files;
    }
}
