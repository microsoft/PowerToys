namespace Mages.Core.Runtime
{
    using System;

    /// <summary>
    /// Contains the event data for changes observed in a dictionary.
    /// </summary>
    public class EntryChangedArgs : EventArgs
    {
        /// <summary>
        /// Creates a new event data container.
        /// </summary>
        public EntryChangedArgs(String key, Object oldValue, Object newValue)
        {
            Key = key;
            OldValue = oldValue;
            NewValue = newValue;
        }

        /// <summary>
        /// Gets the key that changed.
        /// </summary>
        public String Key
        {
            get;
            private set;
        }

        /// <summary>
        /// Gets the previously assigned value, if any.
        /// </summary>
        public Object OldValue
        {
            get;
            private set;
        }

        /// <summary>
        /// Gets the currently assigned value, if any.
        /// </summary>
        public Object NewValue
        {
            get;
            private set;
        }
    }
}
