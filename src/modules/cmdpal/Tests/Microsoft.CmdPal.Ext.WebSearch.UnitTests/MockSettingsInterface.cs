// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using Microsoft.CmdPal.Ext.WebSearch.Helpers;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.WebSearch.UnitTests;

public class MockSettingsInterface : ISettingsInterface
{
    private readonly List<HistoryItem> _historyItems;

    public event EventHandler HistoryChanged;

    public bool GlobalIfURI { get; set; }

    public string ShowHistory { get; set; }

    public IReadOnlyList<HistoryItem> HistoryItems => _historyItems;

    public MockSettingsInterface(string showHistory = "none", bool globalIfUri = true, List<HistoryItem> mockHistory = null)
    {
        _historyItems = mockHistory ?? new List<HistoryItem>();
        GlobalIfURI = globalIfUri;
        ShowHistory = showHistory;
    }

    public void AddHistoryItem(HistoryItem historyItem)
    {
        if (historyItem is null)
        {
            return;
        }

        _historyItems.Add(historyItem);

        if (int.TryParse(ShowHistory, out var maxHistoryItems) && maxHistoryItems > 0)
        {
            while (_historyItems.Count > maxHistoryItems)
            {
                _historyItems.RemoveAt(0);
            }
        }

        HistoryChanged?.Invoke(this, EventArgs.Empty);
    }

    // Helper method for testing
    public void ClearHistory()
    {
        _historyItems.Clear();
        HistoryChanged?.Invoke(this, EventArgs.Empty);
    }

    // Helper method for testing
    public int GetHistoryCount()
    {
        return _historyItems.Count;
    }
}
