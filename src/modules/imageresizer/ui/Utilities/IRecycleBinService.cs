// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.ComponentModel;

namespace ImageResizer.Utilities;

/// <summary>
/// Provides operations for interacting with the Windows Recycle Bin.
/// </summary>
internal interface IRecycleBinService
{
    /// <summary>
    /// Queries the Recycle Bin for a given path's drive.
    /// </summary>
    /// <param name="path">The file path to query. The path root will be automatically extracted.
    /// Pass <c>null</c> to query all drives.
    /// </param>
    /// <returns>The Recycle Bin information, including number of files and size, or <c>null</c> if
    /// the drive has no Recycle Bin.</returns>
    /// <exception cref="ArgumentException">Thrown if the drive root for
    /// <paramref name="path"/> cannot be determined.</exception>
    /// <exception cref="Win32Exception">Thrown if the underlying API call
    /// returned a failure code.</exception>
    RecycleBinInfo QueryRecycleBin(string path = null);

    /// <summary>
    /// Deletes a file by moving it to the Recycle Bin.
    /// </summary>
    /// <param name="filePath">The path to the file to delete.</param>
    /// <exception cref="NoRecycleBinException">Thrown if the drive lacks a Recycle Bin.</exception>
    /// <remarks>You may call <see cref="HasRecycleBin(string)"/> beforehand to confirm that the
    /// target exists on a drive with a Recycle Bin present.</remarks>
    void DeleteToRecycleBin(string filePath);

    /// <summary>
    /// Checks if a path is on a drive which has a Recycle Bin available.
    /// </summary>
    /// <param name="path">The path to check.</param>
    /// <returns><c>true</c> if the drive has a Recycle Bin; otherwise <c>false</c>.</returns>
    /// <exception cref="ArgumentException">Thrown if the drive root for
    /// <paramref name="path"/> cannot be determined.</exception>
    /// <exception cref="Win32Exception">Thrown if the underlying API call
    /// returned a failure code.</exception>
    bool HasRecycleBin(string path);
}
