// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using Microsoft.CmdPal.Ext.WebSearch.Commands;
using Microsoft.CmdPal.Ext.WebSearch.Helpers;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.WebSearch.UnitTests;

public class MockSettingsInterface : ISettingsInterface
{
    private readonly List<HistoryItem> _historyItems;

    public bool GlobalIfURI { get; set; }

    public string ShowHistory { get; set; }

    public MockSettingsInterface(string showHistory = "none", bool globalIfUri = true, List<HistoryItem> mockHistory = null)
    {
        _historyItems = mockHistory ?? new List<HistoryItem>();
        GlobalIfURI = globalIfUri;
        ShowHistory = showHistory;
    }

    public List<ListItem> LoadHistory()
    {
        var listItems = new List<ListItem>();
        foreach (var historyItem in _historyItems)
        {
            listItems.Add(new ListItem(new SearchWebCommand(historyItem.SearchString, this))
            {
                Title = historyItem.SearchString,
                Subtitle = historyItem.Timestamp.ToString("g", System.Globalization.CultureInfo.InvariantCulture),
            });
        }

        listItems.Reverse();
        return listItems;
    }

    public void SaveHistory(HistoryItem historyItem)
    {
        if (historyItem is null)
        {
            return;
        }

        _historyItems.Add(historyItem);

        // Simulate the same logic as SettingsManager
        if (int.TryParse(ShowHistory, out var maxHistoryItems) && maxHistoryItems > 0)
        {
            while (_historyItems.Count > maxHistoryItems)
            {
                _historyItems.RemoveAt(0); // Remove the oldest item
            }
        }
    }

    // Helper method for testing
    public void ClearHistory()
    {
        _historyItems.Clear();
    }

    // Helper method for testing
    public int GetHistoryCount()
    {
        return _historyItems.Count;
    }
}
