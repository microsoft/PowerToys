// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Serialization;

namespace Microsoft.CmdPal.Ext.Bookmarks.Models;

public class BookmarkData
{
    public string Name { get; set; } = string.Empty;

    public string Bookmark { get; set; } = string.Empty;

    [JsonConverter(typeof(JsonStringEnumConverter<BookmarkType>))]
    public BookmarkType Type { get; set; } = BookmarkType.Web;

    [JsonIgnore]
    public bool IsPlaceholder => Bookmark.Contains('{') && Bookmark.Contains('}');
}
