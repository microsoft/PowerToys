// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.WebSearch.Helpers;

public interface ISettingsInterface
{
    event EventHandler? HistoryChanged;

    public bool GlobalIfURI { get; }

    public int HistoryItemCount { get; }

    public IReadOnlyList<HistoryItem> HistoryItems { get; }

    string CustomSearchUri { get; }

    public void AddHistoryItem(HistoryItem historyItem);
}
