using NLog.Filters;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls.Primitives;
using Wox.Infrastructure;
using Wox.Infrastructure.Logger;

namespace Wox.Infrastructure.Storage
{
    /// <summary>
    /// The intent of this class is to provide a basic subset of 'list' like operations, without exposing callers to the internal representation
    /// of the data structure.  Currently this is implemented as a list for it's simplicity. 
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class ListRepository<T> : IRepository<T>, IEnumerable<T>
    {
        public IList<T> Items { get { return _items.Values.ToList(); } }

        private ConcurrentDictionary<int, T> _items = new ConcurrentDictionary<int, T>();

        public ListRepository()
        {
           
        }

        public void Set(IList<T> items)
        {
            //enforce that internal representation
            try
            {
                _items = new ConcurrentDictionary<int, T>(items.ToDictionary(i => i.GetHashCode()));
            }
            catch(ArgumentException e)
            {
                Log.Info($"|LisRepository.Set| Trying to insert a duplicate item", e.Message);
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
                Log.Error($"|ListRepository.Add| Item Already Exists <{insertedItem}>");
            }
     
        }

        public void Remove(T removedItem)
        {
            if (!_items.TryRemove(removedItem.GetHashCode(), out _))
            {
                Log.Error($"|ListRepository.Remove| Item Not Found <{removedItem}>");
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
