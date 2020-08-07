// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Wox.Infrastructure.Storage
{
    public interface IStorage<T>
    {
        /// <summary>
        /// Saves the data
        /// </summary>
        /// <param name="data">data to be saved</param>
        void Save(T data);

        /// <summary>
        /// Attempts to load data, otherwise it will return the default provided
        /// </summary>
        /// <param name="defaultData">default data value</param>
        /// <returns>The loaded data or default</returns>
        T TryLoad(T defaultData);
    }
}
