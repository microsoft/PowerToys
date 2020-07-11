using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Wox.Infrastructure.Storage
{
    public interface IStorage<T>
    {
        /// <summary>
        /// Saves the data
        /// </summary>
        /// <param name="data"></param>
        void Save(T data);

        /// <summary>
        /// Attempts to load data, otherwise it will return the default provided
        /// </summary>
        /// <param name="defaultData"></param>
        /// <returns>The loaded data or default</returns>
        T TryLoad(T defaultData);
    }
}
