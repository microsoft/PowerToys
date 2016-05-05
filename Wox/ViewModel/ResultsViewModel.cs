using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using Wox.Core.UserSettings;
using Wox.Plugin;
using Wox.Storage;

namespace Wox.ViewModel
{
    public class ResultsViewModel : BaseViewModel
    {
        #region Private Fields

        private ResultViewModel _selectedResult;
        public ResultCollection Results { get; }
        private Thickness _margin;

        private readonly object _addResultsLock = new object();
        private readonly object _collectionLock = new object();
        private readonly Settings _settings;

        public ResultsViewModel(Settings settings)
        {
            _settings = settings;
            Results = new ResultCollection();
            BindingOperations.EnableCollectionSynchronization(Results, _collectionLock);
        }

        #endregion

        #region ViewModel Properties

        public int MaxHeight => _settings.MaxResultsToShow * 50;

        public ResultViewModel SelectedResult
        {
            get
            {
                return _selectedResult;
            }
            set
            {
                if (value != null)
                {
                    if (_selectedResult != null)
                    {
                        _selectedResult.IsSelected = false;
                    }

                    _selectedResult = value;

                    if (_selectedResult != null)
                    {
                        _selectedResult.IsSelected = true;
                    }

                }

                OnPropertyChanged();

            }
        }

        public Thickness Margin
        {
            get
            {
                return _margin;
            }
            set
            {
                _margin = value;
                OnPropertyChanged();
            }
        }

        #endregion

        #region Private Methods

        private int InsertIndexOf(int newScore, IList<ResultViewModel> list)
        {
            int index = 0;
            for (; index < list.Count; index++)
            {
                var result = list[index];
                if (newScore > result.RawResult.Score)
                {
                    break;
                }
            }
            return index;
        }

        #endregion

        #region Public Methods

        public void SelectResult(int index)
        {
            if (index <= Results.Count - 1)
            {
                SelectedResult = Results[index];
            }
        }

        public void SelectResult(ResultViewModel result)
        {
            int i = Results.IndexOf(result);
            SelectResult(i);
        }

        public void SelectNextResult()
        {
            if (SelectedResult != null)
            {
                var index = Results.IndexOf(SelectedResult);
                if (index == Results.Count - 1)
                {
                    index = -1;
                }
                SelectedResult = Results.ElementAt(index + 1);
            }
        }

        public void SelectPrevResult()
        {
            if (SelectedResult != null)
            {
                var index = Results.IndexOf(SelectedResult);
                if (index == 0)
                {
                    index = Results.Count;
                }
                SelectedResult = Results.ElementAt(index - 1);
            }
        }

        public void SelectNextPage()
        {
            var index = 0;
            if (SelectedResult != null)
            {
                index = Results.IndexOf(SelectedResult);
            }
            index += 5;
            if (index > Results.Count - 1)
            {
                index = Results.Count - 1;
            }
            SelectedResult = Results.ElementAt(index);
        }

        public void SelectPrevPage()
        {
            var index = 0;
            if (SelectedResult != null)
            {
                index = Results.IndexOf(SelectedResult);
            }
            index -= 5;
            if (index < 0)
            {
                index = 0;
            }
            SelectedResult = Results.ElementAt(index);
        }

        public void Clear()
        {
            Results.Clear();
        }

        public void RemoveResultsExcept(PluginMetadata metadata)
        {
            Results.RemoveAll(r => r.RawResult.PluginID != metadata.ID);
        }

        public void RemoveResultsFor(PluginMetadata metadata)
        {
            Results.RemoveAll(r => r.PluginID == metadata.ID);
        }

        /// <summary>
        /// To avoid deadlock, this method should not called from main thread
        /// </summary>
        public void AddResults(List<Result> newRawResults, string resultId)
        {
            lock (_addResultsLock)
            {
                var newResults = newRawResults.Select(r => new ResultViewModel(r)).ToList();
                // todo use async to do new result calculation
                var resultsCopy = Results.ToList();
                var oldResults = resultsCopy.Where(r => r.PluginID == resultId).ToList();

                // intersection of A (old results) and B (new newResults)
                var intersection = oldResults.Intersect(newResults).ToList();

                // remove result of relative complement of B in A
                foreach (var result in oldResults.Except(intersection))
                {
                    resultsCopy.Remove(result);
                }

                // update index for result in intersection of A and B
                foreach (var commonResult in intersection)
                {
                    int oldIndex = resultsCopy.IndexOf(commonResult);
                    int oldScore = resultsCopy[oldIndex].Score;
                    var newResult = newResults[newResults.IndexOf(commonResult)];
                    int newScore = newResult.Score;
                    if (newScore != oldScore)
                    {
                        var oldResult = resultsCopy[oldIndex];

                        oldResult.Score = newScore;
                        oldResult.OriginQuery = newResult.OriginQuery;

                        resultsCopy.RemoveAt(oldIndex);
                        int newIndex = InsertIndexOf(newScore, resultsCopy);
                        resultsCopy.Insert(newIndex, oldResult);
                    }
                }

                // insert result in relative complement of A in B
                foreach (var result in newResults.Except(intersection))
                {
                    int newIndex = InsertIndexOf(result.Score, resultsCopy);
                    resultsCopy.Insert(newIndex, result);
                }

                // update UI in one run, so it can avoid UI flickering
                Results.Update(resultsCopy);

                if (Results.Count > 0)
                {
                    Margin = new Thickness { Top = 8 };
                    SelectedResult = Results[0];
                }
                else
                {
                    Margin = new Thickness { Top = 0 };
                }
            }
        }


        #endregion

        public class ResultCollection : ObservableCollection<ResultViewModel>
        {

            public void RemoveAll(Predicate<ResultViewModel> predicate)
            {
                CheckReentrancy();

                List<ResultViewModel> itemsToRemove = Items.Where(x => predicate(x)).ToList();
                if (itemsToRemove.Count > 0)
                {
                    itemsToRemove.ForEach(item => { Items.Remove(item); });

                    OnPropertyChanged(new PropertyChangedEventArgs("Count"));
                    OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
                    // fuck ms 
                    // http://blogs.msdn.com/b/nathannesbit/archive/2009/04/20/addrange-and-observablecollection.aspx
                    // http://geekswithblogs.net/NewThingsILearned/archive/2008/01/16/listcollectionviewcollectionview-doesnt-support-notifycollectionchanged-with-multiple-items.aspx
                    // PS: don't use Reset for other data updates, it will cause UI flickering
                    OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
                }


            }

            public void Update(List<ResultViewModel> newItems)
            {
                int newCount = newItems.Count;
                int oldCount = Items.Count;
                int location = newCount > oldCount ? oldCount : newCount;
                for (int i = 0; i < location; i++)
                {
                    ResultViewModel oldResult = Items[i];
                    ResultViewModel newResult = newItems[i];
                    if (!oldResult.Equals(newResult))
                    {

                        this[i] = newResult;
                    }
                    else if (oldResult.Score != newResult.Score)
                    {
                        this[i].Score = newResult.Score;
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

}
