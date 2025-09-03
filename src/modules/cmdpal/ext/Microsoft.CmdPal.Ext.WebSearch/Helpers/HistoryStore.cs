// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using ManagedCommon;

namespace Microsoft.CmdPal.Ext.WebSearch.Helpers;

internal sealed class HistoryStore
{
    private readonly string _filePath;
    private readonly List<HistoryItem> _items = [];
    private readonly Lock _lock = new();

    private int _capacity;

    public event EventHandler? Changed;

    public HistoryStore(string filePath, int capacity)
    {
        ArgumentNullException.ThrowIfNull(filePath);
        ArgumentOutOfRangeException.ThrowIfNegative(capacity);

        _filePath = filePath;
        _capacity = capacity;

        _items.AddRange(LoadFromDiskSafe());
        TrimNoLock();
    }

    public IReadOnlyList<HistoryItem> HistoryItems
    {
        get
        {
            lock (_lock)
            {
                return _items.ToList();
            }
        }
    }

    public void Add(HistoryItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        lock (_lock)
        {
            _items.Add(item);
            _ = TrimNoLock();
            SaveNoLock();
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void SetCapacity(int capacity)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(capacity);

        bool trimmed;
        lock (_lock)
        {
            _capacity = capacity;
            trimmed = TrimNoLock();
            if (trimmed)
            {
                SaveNoLock();
            }
        }

        if (trimmed)
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    private bool TrimNoLock()
    {
        var max = _capacity;
        if (_items.Count > max)
        {
            _items.RemoveRange(0, _items.Count - max);
            return true;
        }

        return false;
    }

    private List<HistoryItem> LoadFromDiskSafe()
    {
        try
        {
            if (!File.Exists(_filePath))
            {
                return [];
            }

            var fileContent = File.ReadAllText(_filePath);
            var historyItems = JsonSerializer.Deserialize<List<HistoryItem>>(fileContent, WebSearchJsonSerializationContext.Default.ListHistoryItem) ?? [];
            return historyItems;
        }
        catch (Exception ex)
        {
            Logger.LogError("Unable to load history", ex);
            return [];
        }
    }

    private void SaveNoLock()
    {
        var json = JsonSerializer.Serialize(_items, WebSearchJsonSerializationContext.Default.ListHistoryItem);
        File.WriteAllText(_filePath, json);
    }
}
