// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Microsoft.CmdPal.Ext.Bookmarks;

public sealed class Bookmarks
{
    public List<BookmarkData> Data { get; set; } = [];
}
