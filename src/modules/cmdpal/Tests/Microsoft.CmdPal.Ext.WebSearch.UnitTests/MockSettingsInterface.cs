// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using Microsoft.CmdPal.Ext.WebSearch.Helpers;

namespace Microsoft.CmdPal.Ext.WebSearch.UnitTests;

public class MockSettingsInterface : ISettingsInterface
{
    private readonly List<HistoryItem> _historyItems;

    public event EventHandler HistoryChanged;

    public bool GlobalIfURI { get; set; }

    public int HistoryItemCount { get; set; }

    public string CustomSearchUri { get; }

    public IReadOnlyList<HistoryItem> HistoryItems => _historyItems;

    public MockSettingsInterface(int historyItemCount = 0, bool globalIfUri = true, List<HistoryItem> mockHistory = null)
    {
        _historyItems = mockHistory ?? new List<HistoryItem>();
        GlobalIfURI = globalIfUri;
        HistoryItemCount = historyItemCount;
    }

    public void AddHistoryItem(HistoryItem historyItem)
    {
        if (historyItem is null)
        {
            return;
        }

        _historyItems.Add(historyItem);

        // Simulate the same logic as SettingsManager
        if (HistoryItemCount > 0)
        {
            while (_historyItems.Count > HistoryItemCount)
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
