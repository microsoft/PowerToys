namespace Mages.Core.Runtime
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// Represents the observable dictionary from MAGES.
    /// </summary>
    public class ObservableDictionary : IDictionary<String, Object>
    {
        #region Fields

        private readonly IDictionary<String, Object> _container;

        #endregion

        #region Events

        /// <summary>
        /// Fired once an element is added, removed, or updated.
        /// </summary>
        public event EventHandler<EntryChangedArgs> Changed;

        #endregion

        #region ctor

        /// <summary>
        /// Creates a new observable dictionary with a standard container.
        /// </summary>
        public ObservableDictionary()
            : this(new Dictionary<String, Object>())
        {
        }

        /// <summary>
        /// Creates a new observable dictionary with the given container.
        /// </summary>
        public ObservableDictionary(IDictionary<String, Object> container)
        {
            _container = container;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets the used container.
        /// </summary>
        public IDictionary<String, Object> Container
        {
            get { return _container; }
        }

        #endregion

        #region IDictionary Implementation

        /// <summary>
        /// Gets or sets the value at key.
        /// </summary>
        public Object this[String key]
        {
            get { return _container[key]; }
            set
            {
                var existing = default(Object);
                _container.TryGetValue(key, out existing);
                _container[key] = value;
                Emit(key, existing, value);
            }
        }

        /// <summary>
        /// Gets the number of items stored in the container.
        /// </summary>
        public Int32 Count
        {
            get { return _container.Count; }
        }

        Boolean ICollection<KeyValuePair<String, Object>>.IsReadOnly
        {
            get { return _container.IsReadOnly; }
        }

        ICollection<String> IDictionary<String, Object>.Keys
        {
            get { return _container.Keys; }
        }

        ICollection<Object> IDictionary<String, Object>.Values
        {
            get { return _container.Values; }
        }

        void ICollection<KeyValuePair<String, Object>>.Add(KeyValuePair<String, Object> item)
        {
            _container.Add(item);
            Emit(item.Key, null, item.Value);
        }

        /// <summary>
        /// Adds the given key, value pair to the container.
        /// </summary>
        public void Add(String key, Object value)
        {
            _container.Add(key, value);
            Emit(key, null, value);
        }

        /// <summary>
        /// Resets the container.
        /// </summary>
        public void Clear()
        {
            var items = _container.ToArray();
            
            foreach (var item in items)
            {
                _container.Remove(item);
                Emit(item.Key, item.Value, null);
            }
        }

        Boolean ICollection<KeyValuePair<String, Object>>.Contains(KeyValuePair<String, Object> item)
        {
            return _container.Contains(item);
        }

        Boolean IDictionary<String, Object>.ContainsKey(String key)
        {
            return _container.ContainsKey(key);
        }

        void ICollection<KeyValuePair<String, Object>>.CopyTo(KeyValuePair<String, Object>[] array, Int32 arrayIndex)
        {
            _container.CopyTo(array, arrayIndex);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return _container.GetEnumerator();
        }

        IEnumerator<KeyValuePair<String, Object>> IEnumerable<KeyValuePair<String, Object>>.GetEnumerator()
        {
            return _container.GetEnumerator();
        }

        Boolean ICollection<KeyValuePair<String, Object>>.Remove(KeyValuePair<String, Object> item)
        {
            if (_container.Contains(item))
            {
                _container.Remove(item);
                Emit(item.Key, item.Value, null);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Removes the given key from the container.
        /// </summary>
        public Boolean Remove(String key)
        {
            var existing = default(Object);

            if (_container.TryGetValue(key, out existing))
            {
                _container.Remove(key);
                Emit(key, existing, null);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Tries to get the value at the given key.
        /// </summary>
        public Boolean TryGetValue(String key, out Object value)
        {
            return _container.TryGetValue(key, out value);
        }

        #endregion

        #region Helpers

        /// <summary>
        /// Called after something changed.
        /// </summary>
        /// <param name="key">The name of the changed entry.</param>
        /// <param name="oldValue">The entry's previous value.</param>
        /// <param name="newValue">The entry's new value.</param>
        protected virtual void OnChanged(String key, Object oldValue, Object newValue)
        {
        }

        private void Emit(String key, Object oldValue, Object newValue)
        {
            var handler = Changed;

            if (handler != null)
            {
                handler.Invoke(this, new EntryChangedArgs(key, oldValue, newValue));
            }

            OnChanged(key, oldValue, newValue);
        }

        #endregion
    }
}
