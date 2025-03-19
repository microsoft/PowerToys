// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.CmdPal.Ext.Apps.Storage;

/// <summary>
/// The intent of this class is to provide a basic subset of 'list' like operations, without exposing callers to the internal representation
/// of the data structure.  Currently this is implemented as a list for it's simplicity.
/// </summary>
/// <typeparam name="T">typeof</typeparam>
public class ListRepository<T> : IRepository<T>, IEnumerable<T>
{
    public IList<T> Items
    {
        get { return _items.Values.ToList(); }
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
#pragma warning disable CS8602 // Dereference of a possibly null reference.
            _items = new ConcurrentDictionary<int, T>(list.ToDictionary(i => i.GetHashCode()));
#pragma warning restore CS8602 // Dereference of a possibly null reference.
        }
        catch (ArgumentException)
        {
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

    public ParallelQuery<T> AsParallel()
    {
        return _items.Values.AsParallel();
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
