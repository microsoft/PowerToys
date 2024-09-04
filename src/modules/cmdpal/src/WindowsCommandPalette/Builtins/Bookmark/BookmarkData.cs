// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.Windows.CommandPalette.Extensions;
using Microsoft.Windows.CommandPalette.Extensions.Helpers;
using Windows.Foundation;
using Windows.System;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Run.Bookmarks;

public class BookmarkData
{
    public string Name = string.Empty;
    public string Bookmark = string.Empty;
    public string Type = string.Empty;
}
