// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using ImageResizer.Utilities;

namespace ImageResizer.Models;

/// <summary>
/// Unit testing mock <see cref="IRecycleBinService"/> implementation.
/// </summary>
internal sealed class MockWindowsRecycleBinService : IRecycleBinService
{
    private readonly Dictionary<string, bool> _driveRecycleBinInfo = [];

    private readonly List<string> _deletedFiles = [];

    /// <summary>
    /// Gets or sets a mock result for a single Recycle Bin. The default result is one containing
    /// no files.
    /// </summary>
    public RecycleBinInfo SingleDriveQueryResult { get; set; } = new RecycleBinInfo(0, 0);

    /// <summary>
    /// Gets or sets a multiple-drive result. The default result is one containing a single file of
    /// 1 byte.
    /// </summary>
    public RecycleBinInfo AllDrivesQueryResult { get; set; } = new RecycleBinInfo(1, 1);

    /// <summary>
    /// Gets the list of files which have been deleted to the Recycle Bin.
    /// </summary>
    public IReadOnlyList<string> DeletedFiles => _deletedFiles.AsReadOnly();

    /// <summary>
    /// Sets whether a drive has a Recycle Bin present. Use for testing QueryRecycleBin
    /// functionality.
    /// </summary>
    /// <param name="driveLetter">The drive letter, e.g. "C".</param>
    /// <param name="hasRecycleBin">Whether the mocked drive will present as having a Recycle Bin
    /// when queried.</param>
    public void SetDriveHasRecycleBin(string driveLetter, bool hasRecycleBin)
    {
        string drive = driveLetter.TrimEnd(':', '\\') + @":\";
        _driveRecycleBinInfo[drive] = hasRecycleBin;
    }

    /// <summary>
    /// Queries the mocked Recycle Bin for the specified drive or all drives if no path is provided.
    /// </summary>
    /// <param name="path">The path to the drive to query, or to a file on that drive. If
    /// <see langword="null"/>, queries all drives.</param>
    /// <returns>A <see cref="RecycleBinInfo"/> object containing information about the Recycle Bin
    /// contents. Returns a result for all drives if <paramref name="path"/> is
    /// <see langword="null"/>. Returns <see langword="null"/> if there is no Recycle Bin on the
    /// specified path.</returns>
    /// <exception cref="ArgumentException">Thrown if the <paramref name="path"/> is
    /// <see langword="null"/>, empty, or refers to a drive which has not been configured.
    /// </exception>
    public RecycleBinInfo QueryRecycleBin(string path = null)
    {
        if (path is null)
        {
            return AllDrivesQueryResult;
        }

        string root = Path.GetPathRoot(path);

        if (string.IsNullOrEmpty(root))
        {
            throw new ArgumentException(
                $"Cannot determine drive root from path '{path}'.",
                nameof(path));
        }

        if (_driveRecycleBinInfo.TryGetValue(root, out bool hasRecycleBin))
        {
            return hasRecycleBin ? SingleDriveQueryResult : null;
        }
        else
        {
            // Models a non-existent real world drive.
            return null;
        }
    }

    /// <summary>
    /// Moves the specified file to the Recycle Bin.
    /// </summary>
    /// <param name="filePath">The path of the file to be deleted. Must not be null or empty.
    /// </param>
    /// <exception cref="NoRecycleBinException">Thrown if the drive containing the file does not
    /// have a Recycle Bin.</exception>
    public void DeleteToRecycleBin(string filePath)
    {
        if (QueryRecycleBin(filePath) is null)
        {
            throw new NoRecycleBinException(
                $"Drive does not have a Recycle Bin.");
        }

        _deletedFiles.Add(filePath);
    }

    /// <summary>
    /// Determines whether the specified path has an associated Recycle Bin.
    /// </summary>
    /// <param name="path">The file system path to check for an associated Recycle Bin.</param>
    /// <returns><see langword="true"/> if the specified path's drive has a Recycle Bin; otherwise,
    /// <see langword="false"/>.</returns>
    public bool HasRecycleBin(string path) => QueryRecycleBin(path) is not null;

    /// <summary>
    /// Resets the state by clearing all tracked deleted files and drives information.
    /// </summary>
    public void Reset()
    {
        _deletedFiles.Clear();
        _driveRecycleBinInfo.Clear();
    }
}
