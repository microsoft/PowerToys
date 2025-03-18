// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.PowerToys.Settings.UI.Helpers
{
#pragma warning disable SA1649 // File name should match first type name
    public class IndexedItem<T>
#pragma warning restore SA1649 // File name should match first type name
    {
        public T Item { get; set; }

        public int Index { get; set; }

        public IndexedItem(T item, int index)
        {
            Item = item;
            Index = index;
        }
    }

#pragma warning disable SA1402 // File may only contain a single type
    public partial class IndexedObservableCollection<T> : ObservableCollection<IndexedItem<T>>
#pragma warning restore SA1402 // File may only contain a single type
    {
        public IndexedObservableCollection(IEnumerable<T> items)
        {
            int index = 0;
            foreach (var item in items)
            {
                Add(new IndexedItem<T>(item, index++));
            }
        }

        public IEnumerable<T> ToEnumerable()
        {
            return this.Select(x => x.Item);
        }

        public void Swap(int index1, int index2)
        {
            var temp = this[index1];
            this[index1] = this[index2];
            this[index2] = temp;

            // Update the original index of the items
            this[index1].Index = index1;
            this[index2].Index = index2;
        }
    }
}
