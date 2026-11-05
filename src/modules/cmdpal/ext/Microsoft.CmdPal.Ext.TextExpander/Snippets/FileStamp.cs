// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Security;

namespace Microsoft.CmdPal.Ext.TextExpander.Snippets;

/// <summary>What says a file changed, short of reading it.</summary>
internal readonly record struct FileStamp(bool Exists, long Length, DateTime LastWriteTimeUtc, bool Unreadable)
{
    public static FileStamp Of(string path)
    {
        try
        {
            var info = new FileInfo(path);
            return info.Exists
                ? new FileStamp(true, info.Length, info.LastWriteTimeUtc, false)
                : default;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SecurityException or NotSupportedException or ArgumentException)
        {
            return new FileStamp(false, 0, default, true);
        }
    }
}
