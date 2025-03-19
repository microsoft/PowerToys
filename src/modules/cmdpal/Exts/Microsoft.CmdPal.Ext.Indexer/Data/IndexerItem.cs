// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.IO;

namespace Microsoft.CmdPal.Ext.Indexer.Data;

internal sealed class IndexerItem
{
    internal string FullPath { get; init; }

    internal string FileName { get; init; }

    internal bool IsDirectory()
    {
        if (!Path.Exists(FullPath))
        {
            return false;
        }

        var attr = File.GetAttributes(FullPath);

        // detect whether it is a directory or file
        return (attr & FileAttributes.Directory) == FileAttributes.Directory;
    }
}
