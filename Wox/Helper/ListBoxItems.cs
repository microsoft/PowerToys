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
    // todo implement custom moveItem,removeItem,insertItem
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
                // fuck ms 
                // http://blogs.msdn.com/b/nathannesbit/archive/2009/04/20/addrange-and-observablecollection.aspx
                // http://geekswithblogs.net/NewThingsILearned/archive/2008/01/16/listcollectionviewcollectionview-doesnt-support-notifycollectionchanged-with-multiple-items.aspx
                // PS: don't use Reset for other data updates, it will cause UI flickering
                OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
            }
        }
    }
}
