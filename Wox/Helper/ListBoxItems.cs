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
    // todo implement custom moveItem,removeItem,insertItem for better performance
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

        public void Update(List<Result> newItems)
        {
            int newCount = newItems.Count;
            int oldCount = Items.Count;
            int location = newCount > oldCount ? oldCount : newCount;
            for (int i = 0; i < location; i++)
            {
                Result oldItem = Items[i];
                Result newItem = newItems[i];
                if (!oldItem.Equals(newItem))
                {
                    this[i] = newItem;
                }
                else if (oldItem.Score != newItem.Score)
                {
                    this[i].Score = newItem.Score;
                }
            }

            if (newCount > oldCount)
            {
                for (int i = oldCount; i < newCount; i++)
                {
                    Add(newItems[i]);
                }
            }
            else
            {
                int removeIndex = newCount;
                for (int i = newCount; i < oldCount; i++)
                {
                    RemoveAt(removeIndex);
                }
            }

        }
    }
}
