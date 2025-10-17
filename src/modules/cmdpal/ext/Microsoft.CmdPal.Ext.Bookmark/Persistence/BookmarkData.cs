// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace Microsoft.CmdPal.Ext.Bookmarks.Persistence;

public sealed record BookmarkData
{
    public Guid Id { get; init; }

    public required string Name { get; init; }

    public required string Bookmark { get; init; }

    [JsonConstructor]
    [SetsRequiredMembers]
    public BookmarkData(Guid id, string? name, string? bookmark)
    {
        Id = id == Guid.Empty ? Guid.NewGuid() : id;
        Name = name ?? string.Empty;
        Bookmark = bookmark ?? string.Empty;
    }

    [SetsRequiredMembers]
    public BookmarkData(string? name, string? bookmark)
        : this(Guid.NewGuid(), name, bookmark)
    {
    }

    [SetsRequiredMembers]
    public BookmarkData()
        : this(Guid.NewGuid(), string.Empty, string.Empty)
    {
    }
}
