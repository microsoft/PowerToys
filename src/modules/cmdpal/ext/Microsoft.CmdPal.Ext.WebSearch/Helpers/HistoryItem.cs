// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Text.Json;

namespace Microsoft.CmdPal.Ext.WebSearch.Helpers;

public class HistoryItem(string searchString, DateTime timestamp)
{
    public string SearchString { get; private set; } = searchString;

    public DateTime Timestamp { get; private set; } = timestamp;

    public string ToJson() => JsonSerializer.Serialize(this);
}
