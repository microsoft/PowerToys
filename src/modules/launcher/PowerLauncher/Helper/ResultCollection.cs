using PowerLauncher.ViewModel;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace PowerLauncher.Helper
{
    public class ResultCollection : ObservableCollection<ResultViewModel>
    {

        public void RemoveAll(Predicate<ResultViewModel> predicate)
        {
            CheckReentrancy();

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
            if(newItems == null)
            {
                throw new ArgumentNullException(nameof(newItems));
            }
            
            int newCount = newItems.Count;
            int oldCount = Items.Count;
            int location = newCount > oldCount ? oldCount : newCount;

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
    }
}
