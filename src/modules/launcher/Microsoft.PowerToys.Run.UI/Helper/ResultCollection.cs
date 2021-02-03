// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Collections.Specialized;
using Microsoft.PowerToys.Run.UI.ViewModel;

namespace Microsoft.PowerToys.Run.UI.Helper
{
    public class ResultCollection : List<ResultViewModel>, INotifyCollectionChanged
    {
        public ResultCollection()
        {
        }

        public ResultCollection(IEnumerable<ResultViewModel> rvm)
            : base(rvm)
        {
        }

        public event NotifyCollectionChangedEventHandler CollectionChanged;

        /// <summary>
        /// Notify change in the List view items
        /// </summary>
        /// <param name="e">The event argument.</param>
        public void NotifyChanges()
        {
            CollectionChanged(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }
    }
}
