// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics.CodeAnalysis;

namespace Microsoft.CmdPal.Ext.Bookmarks.Persistence;

public sealed class BookmarksData
{
    [SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "This JSON DTO retains its writable deserialization contract, including existing omitted-member defaults and explicit-null behavior.")]
    public List<BookmarkData> Data { get; set; } = [];
}
