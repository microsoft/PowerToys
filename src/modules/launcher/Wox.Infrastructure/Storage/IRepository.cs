using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Wox.Infrastructure.Storage
{
    public interface IRepository<T>
    {
        void Add(T insertedItem);
        void Remove(T removedItem);
        bool Contains(T item);
        void Set(IList<T> list);
        bool Any();
    }
}
