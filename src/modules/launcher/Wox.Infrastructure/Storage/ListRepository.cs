using NLog.Filters;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Wox.Infrastructure;

namespace Wox.Infrastructure.Storage
{
    /// <summary>
    /// The intent of this class is to provide a basic subset of 'list' like operations, without exposing callers to the internal representation
    /// of the data structure.  Currently this is implemented as a list for it's simplicity. 
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class ListRepository<T> : IRepository<T>, IEnumerable<T>
    {
        protected  IList<T> _items = new List<T>();
        protected  IStorage<IList<T>> _storage;

        public ListRepository(IStorage<IList<T>> storage)
        {
            _storage = storage ?? throw new ArgumentNullException("storage", "StorageRepository requires an initialized storage interface");
        }

        public void Set(IList<T> items)
        {
            //enforce that internal representation
            _items = items.ToList<T>();
        }

        public bool Any()
        {
            return _items.Any();
        }

        public void Add(T insertedItem)
        {
            _items.Add(insertedItem);
        }

        public void Remove(T removedItem)
        {
            _items.Remove(removedItem);
        }

        public ParallelQuery<T> AsParallel()
        {
            return _items.AsParallel();
        }

        public bool Contains(T item)
        {
            return _items.Contains(item);
        }

        public IEnumerator<T> GetEnumerator()
        {
            return _items.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return _items.GetEnumerator();
        }
    }
}
