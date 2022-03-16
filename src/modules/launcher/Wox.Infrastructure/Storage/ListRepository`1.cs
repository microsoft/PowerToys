// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Wox.Plugin.Logger;

namespace Wox.Infrastructure.Storage
{
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
                _items = new ConcurrentDictionary<int, T>(list.ToDictionary(i => i.GetHashCode()));
            }
            catch (ArgumentException e)
            {
                Log.Info($"Trying to insert a duplicate item {e.Message}", GetType());
            }
        }

        public bool Any()
        {
            return _items.Any();
        }

        public void Add(T insertedItem)
        {
            if (!_items.TryAdd(insertedItem.GetHashCode(), insertedItem))
            {
                Log.Error($"Item Already Exists <{insertedItem}>", GetType());
            }
        }

        public void Remove(T removedItem)
        {
            if (!_items.TryRemove(removedItem.GetHashCode(), out _))
            {
                Log.Error($"Item Not Found <{removedItem}>", GetType());
            }
        }

        public ParallelQuery<T> AsParallel()
        {
            return _items.Values.AsParallel();
        }

        public bool Contains(T item)
        {
            return _items.ContainsKey(item.GetHashCode());
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
}
