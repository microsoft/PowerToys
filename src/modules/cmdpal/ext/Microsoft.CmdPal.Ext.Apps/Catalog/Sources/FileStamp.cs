// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Security;

namespace Microsoft.CmdPal.Ext.Apps.Catalog.Sources;

/// <summary>Reads file metadata without opening an executable, shortcut or manifest.</summary>
internal readonly record struct FileStamp(bool Exists, long Length, DateTime LastWriteTimeUtc, DateTime CreationTimeUtc, FileAttributes Attributes)
{
    /// <summary>Reads filesystem metadata without opening the file contents.</summary>
    /// <returns>A default stamp for a missing or invalid path, or null when its metadata cannot be read safely.</returns>
    public static FileStamp? TryRead(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path))
        {
            return default(FileStamp);
        }

        try
        {
            var file = new FileInfo(path);
            file.Refresh();
            return file.Exists
                ? new FileStamp(true, file.Length, file.LastWriteTimeUtc, file.CreationTimeUtc, file.Attributes)
                : default(FileStamp);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SecurityException or ArgumentException)
        {
            // An unreadable stamp cannot justify reusing previously parsed metadata.
            return null;
        }
    }
}
