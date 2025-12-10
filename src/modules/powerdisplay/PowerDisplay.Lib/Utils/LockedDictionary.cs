// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;

#pragma warning disable SA1649 // File name should match first type name (generic class name is valid)

namespace PowerDisplay.Common.Utils
{
    /// <summary>
    /// A thread-safe dictionary wrapper that provides atomic operations with minimal locking overhead.
    /// Designed for scenarios where simple get/set operations are common but complex transactions are rare.
    /// For complex multi-step transactions, use <see cref="ExecuteWithLock"/> to ensure atomicity.
    /// </summary>
    /// <typeparam name="TKey">The type of keys in the dictionary.</typeparam>
    /// <typeparam name="TValue">The type of values in the dictionary.</typeparam>
    public class LockedDictionary<TKey, TValue>
        where TKey : notnull
    {
        private readonly Dictionary<TKey, TValue> _dictionary = new();
        private readonly object _lock = new();

        /// <summary>
        /// Tries to get the value associated with the specified key.
        /// </summary>
        /// <param name="key">The key to locate.</param>
        /// <param name="value">When this method returns, contains the value if found; otherwise, the default value.</param>
        /// <returns>True if the key was found; otherwise, false.</returns>
        public bool TryGetValue(TKey key, out TValue? value)
        {
            lock (_lock)
            {
                return _dictionary.TryGetValue(key, out value);
            }
        }

        /// <summary>
        /// Removes all key/value pairs from the dictionary.
        /// </summary>
        public void Clear()
        {
            lock (_lock)
            {
                _dictionary.Clear();
            }
        }

        /// <summary>
        /// Gets a snapshot of all values in the dictionary.
        /// Returns a copy to ensure thread safety.
        /// </summary>
        /// <returns>A list containing copies of all values.</returns>
        public List<TValue> GetValuesSnapshot()
        {
            lock (_lock)
            {
                return new List<TValue>(_dictionary.Values);
            }
        }

        /// <summary>
        /// Executes an action within the lock, providing the internal dictionary for complex operations.
        /// Use this for multi-step transactions that need to be atomic.
        /// WARNING: Do not store or return references to the dictionary outside the action.
        /// </summary>
        /// <param name="action">The action to execute with the dictionary.</param>
        public void ExecuteWithLock(Action<Dictionary<TKey, TValue>> action)
        {
            lock (_lock)
            {
                action(_dictionary);
            }
        }

        /// <summary>
        /// Executes a function within the lock, providing the internal dictionary for complex operations.
        /// Use this for multi-step transactions that need to be atomic and return a result.
        /// WARNING: Do not store or return references to the dictionary outside the function.
        /// </summary>
        /// <typeparam name="TResult">The type of the result.</typeparam>
        /// <param name="func">The function to execute with the dictionary.</param>
        /// <returns>The result of the function.</returns>
        public TResult ExecuteWithLock<TResult>(Func<Dictionary<TKey, TValue>, TResult> func)
        {
            lock (_lock)
            {
                return func(_dictionary);
            }
        }
    }
}
