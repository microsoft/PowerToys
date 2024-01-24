// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;

namespace ColorPicker.Common
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1649:File name should match first type name", Justification = "File name is correct, ignore generics")]
    public sealed class RangeObservableCollection<T> : ObservableCollection<T>
    {
        private object _collectionChangedLock = new object();
        private bool _suppressNotification;

        protected override void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
        {
            lock (_collectionChangedLock)
            {
                if (!_suppressNotification)
                {
                    base.OnCollectionChanged(e);
                }
            }
        }

        public void AddRange(IEnumerable<T> list)
        {
            ArgumentNullException.ThrowIfNull(list);

            _suppressNotification = true;

            foreach (T item in list)
            {
                Add(item);
            }

            _suppressNotification = false;
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }

        public void AddWithoutNotification(T item)
        {
            lock (_collectionChangedLock)
            {
                _suppressNotification = true;
                Add(item);
            }
        }

        public void ReleaseNotification()
        {
            lock (_collectionChangedLock)
            {
                _suppressNotification = false;
                OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
            }
        }

        public void ClearWithoutNotification()
        {
            lock (_collectionChangedLock)
            {
                _suppressNotification = true;
                Clear();
            }
        }
    }
}
