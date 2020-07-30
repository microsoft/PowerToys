using PowerLauncher.ViewModel;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Windows;
using System.Windows.Threading;

namespace PowerLauncher.Helper
{
    public class ResultCollection : ObservableCollection<ResultViewModel>
    {
        /// <summary>
        /// This private variable holds the flag to
        /// turn on and off the collection changed notification.
        /// </summary>
        private bool suspendCollectionChangeNotification;

        /// <summary>
        /// Initializes a new instance of the FastObservableCollection class.
        /// </summary>
        public ResultCollection()
            : base()
        {
            this.suspendCollectionChangeNotification = false;
        }

        /// <summary>
        /// This event is overriden CollectionChanged event of the observable collection.
        /// </summary>
        //public override event NotifyCollectionChangedEventHandler CollectionChanged;

        /// <summary>
        /// Raises collection change event.
        /// </summary>
        public void NotifyChanges()
        {
            this.ResumeCollectionChangeNotification();
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }

        /// <summary>
        /// Resumes collection changed notification.
        /// </summary>
        public void ResumeCollectionChangeNotification()
        {
            this.suspendCollectionChangeNotification = false;
        }

        /// <summary>
        /// Suspends collection changed notification.
        /// </summary>
        public void SuspendCollectionChangeNotification()
        {
            this.suspendCollectionChangeNotification = true;
        }

        /// <summary>
        /// This method removes all items that match a predicate
        /// </summary>
        /// <param name="predicate">predicate</param>
        public void RemovePredicate(Predicate<ResultViewModel> predicate)
        {
            CheckReentrancy();

            this.SuspendCollectionChangeNotification();
            for (int i = Count - 1; i >= 0; i--)
            {
                if (predicate(this[i]))
                {
                    RemoveAt(i);
                }
            }
        }

        /// <summary>
        /// Update the results collection with new results, try to keep identical results
        /// </summary>
        /// <param name="newItems"></param>
        public void Update(List<ResultViewModel> newItems)
        {
            if (newItems == null)
            {
                throw new ArgumentNullException(nameof(newItems));
            }

            int newCount = newItems.Count;
            int oldCount = Items.Count;
            int location = newCount > oldCount ? oldCount : newCount;

            this.SuspendCollectionChangeNotification();
            for (int i = 0; i < location; i++)
            {
                ResultViewModel oldResult = this[i];
                ResultViewModel newResult = newItems[i];
                if (!oldResult.Equals(newResult))
                { // result is not the same update it in the current index
                    this[i] = newResult;
                }
                else if (oldResult.Result.Score != newResult.Result.Score)
                {
                    this[i].Result.Score = newResult.Result.Score;
                }
            }

            if (newCount >= oldCount)
            {
                for (int i = oldCount; i < newCount; i++)
                {
                    Add(newItems[i]);
                }
            }
            else
            {
                for (int i = oldCount - 1; i >= newCount; i--)
                {
                    RemoveAt(i);
                }
            }
        }

        /// <summary>
        /// This collection changed event performs thread safe event raising.
        /// </summary>
        /// <param name="e">The event argument.</param>
        protected override void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
        {
            // Recommended is to avoid reentry 
            // in collection changed event while collection
            // is getting changed on other thread.
            if(!this.suspendCollectionChangeNotification)
            {
                base.OnCollectionChanged(e);
            }
        }
    }
}