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
        /// Gets the number of key/value pairs in the dictionary.
        /// </summary>
        public int Count
        {
            get
            {
                lock (_lock)
                {
                    return _dictionary.Count;
                }
            }
        }

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
        /// Gets the value associated with the specified key, or the default value if not found.
        /// </summary>
        /// <param name="key">The key to locate.</param>
        /// <returns>The value if found; otherwise, the default value.</returns>
        public TValue? GetValueOrDefault(TKey key)
        {
            lock (_lock)
            {
                return _dictionary.TryGetValue(key, out var value) ? value : default;
            }
        }

        /// <summary>
        /// Adds or updates a key/value pair.
        /// </summary>
        /// <param name="key">The key to add or update.</param>
        /// <param name="value">The value to associate with the key.</param>
        public void AddOrUpdate(TKey key, TValue value)
        {
            lock (_lock)
            {
                _dictionary[key] = value;
            }
        }

        /// <summary>
        /// Gets the value for the key if it exists; otherwise, adds the value using the provided factory.
        /// </summary>
        /// <param name="key">The key to locate or add.</param>
        /// <param name="valueFactory">The factory function to create a new value if the key doesn't exist.</param>
        /// <returns>The existing or newly created value.</returns>
        public TValue GetOrAdd(TKey key, Func<TKey, TValue> valueFactory)
        {
            lock (_lock)
            {
                if (_dictionary.TryGetValue(key, out var existingValue))
                {
                    return existingValue;
                }

                var newValue = valueFactory(key);
                _dictionary[key] = newValue;
                return newValue;
            }
        }

        /// <summary>
        /// Tries to remove the value associated with the specified key.
        /// </summary>
        /// <param name="key">The key to remove.</param>
        /// <param name="value">When this method returns, contains the removed value if found; otherwise, the default value.</param>
        /// <returns>True if the key was found and removed; otherwise, false.</returns>
        public bool TryRemove(TKey key, out TValue? value)
        {
            lock (_lock)
            {
                if (_dictionary.TryGetValue(key, out value))
                {
                    _dictionary.Remove(key);
                    return true;
                }

                return false;
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
        /// Checks if the dictionary contains the specified key.
        /// </summary>
        /// <param name="key">The key to check.</param>
        /// <returns>True if the key exists; otherwise, false.</returns>
        public bool ContainsKey(TKey key)
        {
            lock (_lock)
            {
                return _dictionary.ContainsKey(key);
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
        /// Gets a snapshot of all keys in the dictionary.
        /// Returns a copy to ensure thread safety.
        /// </summary>
        /// <returns>A list containing copies of all keys.</returns>
        public List<TKey> GetKeysSnapshot()
        {
            lock (_lock)
            {
                return new List<TKey>(_dictionary.Keys);
            }
        }

        /// <summary>
        /// Gets a snapshot of all key/value pairs in the dictionary.
        /// Returns a copy to ensure thread safety.
        /// </summary>
        /// <returns>A dictionary containing copies of all key/value pairs.</returns>
        public Dictionary<TKey, TValue> GetSnapshot()
        {
            lock (_lock)
            {
                return new Dictionary<TKey, TValue>(_dictionary);
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
