using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using Wox.Plugin;

namespace Wox.Helper
{
    class ListBoxItems : ObservableCollection<Result>
    {
        public void RemoveAll(Predicate<Result> predicate)
        {
            CheckReentrancy();

            List<Result> itemsToRemove = Items.Where(x => predicate(x)).ToList();
            if (itemsToRemove.Count > 0)
            {
                itemsToRemove.ForEach(item => Items.Remove(item));

                OnPropertyChanged(new PropertyChangedEventArgs("Count"));
                OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
                // fuck ms http://blogs.msdn.com/b/nathannesbit/archive/2009/04/20/addrange-and-observablecollection.aspx
                OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
            }
        }
    }
}
