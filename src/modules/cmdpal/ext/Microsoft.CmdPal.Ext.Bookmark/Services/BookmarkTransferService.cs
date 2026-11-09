// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.IO;
using System.Linq;
using Microsoft.CmdPal.Ext.Bookmarks.Persistence;

namespace Microsoft.CmdPal.Ext.Bookmarks.Services;

internal sealed class BookmarkTransferService(IBookmarksManager manager, string? storagePath = null)
{
    public (int Imported, int Skipped) Import(string path)
    {
        var entries = BookmarkTransferFile.Parse(BookmarkTransferFile.Read(path), Path.GetExtension(path));
        var imported = manager.AddRange(entries.Select(entry => (entry.Name, entry.Path)).ToArray());
        return (imported, entries.Count - imported);
    }

    public int Export(string path)
    {
        var bookmarks = manager.Bookmarks;
        BookmarkTransferFile.Export(bookmarks, path, storagePath);
        return bookmarks.Count;
    }
}
