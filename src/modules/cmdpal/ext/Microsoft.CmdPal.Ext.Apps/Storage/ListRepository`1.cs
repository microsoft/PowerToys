// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using ManagedCommon;

namespace Microsoft.CmdPal.Ext.Apps.Storage;

/// <summary>
/// The intent of this class is to provide a basic subset of 'list' like operations, without exposing callers to the internal representation
/// of the data structure.  Currently this is implemented as a list for its simplicity.
/// </summary>
/// <typeparam name="T">typeof</typeparam>
public class ListRepository<T> : IRepository<T>, IEnumerable<T>
{
    public IList<T> Items
    {
        get
        {
            var items = new List<T>(_items.Count);
            foreach (var item in _items.Values)
            {
                items.Add(item);
            }

            return items;
        }
    }

    private ConcurrentDictionary<int, T> _items = new ConcurrentDictionary<int, T>();

    public ListRepository()
    {
    }

    public void SetList(IList<T> list)
    {
        // enforce that internal representation
        try
        {
            var result = new ConcurrentDictionary<int, T>();

            foreach (var item in list)
            {
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                result.TryAdd(item.GetHashCode(), item);
#pragma warning restore CS8602 // Dereference of a possibly null reference.
            }

            _items = result;
        }
        catch (ArgumentException ex)
        {
            Logger.LogInfo(ex.Message);
        }
    }

    public bool Any()
    {
        return !_items.IsEmpty;
    }

    public void Add(T insertedItem)
    {
        if (insertedItem is not null)
        {
            if (!_items.TryAdd(insertedItem.GetHashCode(), insertedItem))
            {
            }
        }
    }

    public void Remove(T removedItem)
    {
        if (removedItem is not null)
        {
            if (!_items.TryRemove(removedItem.GetHashCode(), out _))
            {
            }
        }
    }

    public bool Contains(T item)
    {
        if (item is not null)
        {
            return _items.ContainsKey(item.GetHashCode());
        }

        return false;
    }

    public IEnumerator<T> GetEnumerator()
    {
        return _items.Values.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return _items.GetEnumerator();
    }

    public int Count()
    {
        return _items.Count;
    }
}
