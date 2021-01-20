namespace Mages.Core.Runtime
{
    using Mages.Core.Runtime.Proxies;
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// Represents the object wrapper from MAGES.
    /// </summary>
    public sealed class WrapperObject : IDictionary<String, Object>
    {
        #region Fields
        
        private readonly Object _content;
        private readonly Type _type;
        private readonly Dictionary<String, Object> _extends;
        private readonly Dictionary<String, BaseProxy> _proxies;

        #endregion

        #region ctor

        /// <summary>
        /// Creates a new wrapped instance object.
        /// </summary>
        /// <param name="content">The object to wrap.</param>
        private WrapperObject(Object content)
        {
            _content = content;
            _type = content.GetType();
            _extends = new Dictionary<String, Object>();
            _proxies = _type.GetMemberProxies(this);
        }

        /// <summary>
        /// Creates a new wrapped static object.
        /// </summary>
        /// <param name="type">The type to wrap.</param>
        private WrapperObject(Type type)
        {
            _type = type;
            _content = null;
            _extends = new Dictionary<String, Object>();
            _proxies = type.GetStaticProxies(this);
        }

        #endregion

        #region Create

        /// <summary>
        /// Creates a wrapper object for the given value.
        /// </summary>
        /// <param name="value">The value to inspect.</param>
        /// <returns>The wrapper or null dependent on the value.</returns>
        public static WrapperObject CreateFor(Object value)
        {
            var result = default(WrapperObject);

            if (value != null)
            {
                var type = value as Type;
                result = type != null ? new WrapperObject(type) : new WrapperObject(value);
            }

            return result;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets the wrapped object if instance bound.
        /// </summary>
        public Object Content
        {
            get { return _content; }
        }

        /// <summary>
        /// Gets the type that is wrapped (instance or static).
        /// </summary>
        public Type Type
        {
            get { return _type; }
        }

        #endregion

        #region IDictionary Implementation

        /// <summary>
        /// Gets or sets the value of the underlying object or
        /// the extension object.
        /// </summary>
        /// <param name="key">The name of the property.</param>
        /// <returns>The value of the property.</returns>
        public Object this[String key]
        {
            get
            {
                var result = default(Object);
                TryGetValue(key, out result);
                return result;
            }
            set { TrySetValue(key, value); }
        }

        /// <summary>
        /// Gets the number of properties of the underlying
        /// object and the extension object.
        /// </summary>
        public Int32 Count
        {
            get { return _extends.Count + _proxies.Count; }
        }

        Boolean ICollection<KeyValuePair<String, Object>>.IsReadOnly
        {
            get { return false; }
        }

        /// <summary>
        /// Gets all the keys from the extension object.
        /// </summary>
        public ICollection<String> Keys
        {
            get { return _extends.Keys; }
        }

        /// <summary>
        /// Gets all the values from the extension object.
        /// </summary>
        public ICollection<Object> Values
        {
            get { return _extends.Values; }
        }

        void ICollection<KeyValuePair<String, Object>>.Add(KeyValuePair<String, Object> item)
        {
            Add(item.Key, item.Value);
        }

        /// <summary>
        /// Sets the provided value at the provided property.
        /// </summary>
        /// <param name="key">The name of the property.</param>
        /// <param name="value">The value to use.</param>
        public void Add(String key, Object value)
        {
            TrySetValue(key, value);
        }

        /// <summary>
        /// Resets the extension object.
        /// </summary>
        public void Clear()
        {
            _extends.Clear();
        }

        /// <summary>
        /// Checks if the underlying object or the extension
        /// object contains the given key.
        /// </summary>
        /// <param name="item">The item to check for.</param>
        /// <returns>True if the key is used, otherwise false.</returns>
        public Boolean Contains(KeyValuePair<String, Object> item)
        {
            return ContainsKey(item.Key);
        }

        /// <summary>
        /// Checks if the underlying object or the extension
        /// object contains the given key.
        /// </summary>
        /// <param name="key">The key to check for.</param>
        /// <returns>True if the key is used, otherwise false.</returns>
        public Boolean ContainsKey(String key)
        {
            return _proxies.ContainsKey(key) || _extends.ContainsKey(key);
        }

        void ICollection<KeyValuePair<String, Object>>.CopyTo(KeyValuePair<String, Object>[] array, Int32 arrayIndex)
        {
        }

        /// <summary>
        /// Gets the enumerator over the elements of the extension.
        /// </summary>
        /// <returns>The extension's enumerator.</returns>
        public IEnumerator<KeyValuePair<String, Object>> GetEnumerator()
        {
            return _extends.Concat(_proxies.Select(m => new KeyValuePair<String, Object>(m.Key, m.Value.Value))).GetEnumerator();
        }

        Boolean ICollection<KeyValuePair<String, Object>>.Remove(KeyValuePair<String, Object> item)
        {
            return Remove(item.Key);
        }

        /// <summary>
        /// Removes the item from the extension.
        /// </summary>
        /// <param name="key">The key of the item to be removed.</param>
        /// <returns>True if it could be removed, otherwise false.</returns>
        public Boolean Remove(String key)
        {
            return _extends.Remove(key);
        }

        /// <summary>
        /// Tries to get the value from the given key.
        /// </summary>
        /// <param name="key">The name of the property.</param>
        /// <param name="value">The resulting value.</param>
        /// <returns>True if the value could be retrieved, otherwise false.</returns>
        public Boolean TryGetValue(String key, out Object value)
        {
            var proxy = default(BaseProxy);

            if (_proxies.TryGetValue(key, out proxy))
            {
                value = proxy.Value;
                return true;
            }

            return _extends.TryGetValue(key, out value);
        }

        #endregion

        #region Helpers

        private void TrySetValue(String key, Object value)
        {
            var proxy = default(BaseProxy);
            
            if (_proxies.TryGetValue(key, out proxy))
            {
                proxy.Value = value;
            }
            else
            {
                _extends[key] = value;
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        #endregion
    }
}
