// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.CmdPal.Ext.Calc.Helper;

public sealed class HistoryItem
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Query { get; set; } = string.Empty;

    public string Result { get; set; } = string.Empty;

    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    public HistoryItem()
    {
    }

    public HistoryItem(string query, string result, DateTime timestamp)
    {
        Id = Guid.NewGuid();
        Query = query;
        Result = result;
        Timestamp = timestamp;
    }
}
