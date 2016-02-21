using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using Wox.Core.UserSettings;
using Wox.Plugin;
using Wox.Storage;

namespace Wox.ViewModel
{
    public class ResultsViewModel : BaseViewModel
    {
        #region Private Fields

        private ResultViewModel _selectedResult;
        private ResultCollection _results;
        private bool _isVisible;
        private Thickness _margin;

        private readonly object _resultsUpdateLock = new object();

        #endregion

        #region Constructor

        public ResultsViewModel()
        {
            this._results = new ResultCollection();
        }

        #endregion

        #region ViewModel Properties

        public int MaxHeight
        {
            get
            {
                return UserSettingStorage.Instance.MaxResultsToShow * 50;
            }
        }

        public ResultCollection Results
        {
            get
            {
                return this._results;
            }
        }

        public ResultViewModel SelectedResult
        {
            get
            {
                return this._selectedResult;
            }
            set
            {
                if (null != value)
                {
                    if (null != _selectedResult)
                    {
                        _selectedResult.IsSelected = false;
                    }

                    _selectedResult = value;

                    if (null != _selectedResult)
                    {
                        _selectedResult.IsSelected = true;
                    }

                }

                OnPropertyChanged("SelectedResult");

            }
        }

        public Thickness Margin
        {
            get
            {
                return this._margin;
            }
            set
            {
                this._margin = value;
                OnPropertyChanged("Margin");
            }
        }

        #endregion

        #region Private Methods

        private bool IsTopMostResult(Result result)
        {
            return TopMostRecordStorage.Instance.IsTopMost(result);
        }

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
            if(index <= this.Results.Count - 1)
            {
                this.SelectedResult = this.Results[index];
            }
        }

        public void SelectNextResult()
        {
            if (null != this.SelectedResult)
            {
                var index = this.Results.IndexOf(this.SelectedResult);
                if(index == this.Results.Count - 1)
                {
                    index = -1;
                }
                this.SelectedResult = this.Results.ElementAt(index + 1);
            }
        }

        public void SelectPrevResult()
        {
            if (null != this.SelectedResult)
            {
                var index = this.Results.IndexOf(this.SelectedResult);
                if (index == 0)
                {
                    index = this.Results.Count;
                }
                this.SelectedResult = this.Results.ElementAt(index - 1);
            }
        }

        public void SelectNextPage()
        {
            var index = 0;
            if (null != this.SelectedResult)
            {
                index = this.Results.IndexOf(this.SelectedResult);
            }
            index += 5;
            if (index > this.Results.Count - 1)
            {
                index = this.Results.Count - 1;
            }
            this.SelectedResult = this.Results.ElementAt(index);
        }

        public void SelectPrevPage()
        {
            var index = 0;
            if (null != this.SelectedResult)
            {
                index = this.Results.IndexOf(this.SelectedResult);
            }
            index -= 5;
            if (index < 0)
            {
                index = 0;
            }
            this.SelectedResult = this.Results.ElementAt(index);
        }

        public void Clear()
        {
            this._results.Clear();
        }

        public void RemoveResultsExcept(PluginMetadata metadata)
        {
            lock (_resultsUpdateLock)
            {
                _results.RemoveAll(r => r.RawResult.PluginID != metadata.ID);
            }
        }

        public void RemoveResultsFor(PluginMetadata metadata)
        {
            lock (_resultsUpdateLock)
            {
                _results.RemoveAll(r => r.RawResult.PluginID == metadata.ID);
            }
        }

        public void AddResults(List<Result> newRawResults, string resultId)
        {
            lock (_resultsUpdateLock)
            {
                var newResults = new List<ResultViewModel>();
                newRawResults.ForEach((re) => { newResults.Add(new ResultViewModel(re)); });
                // todo use async to do new result calculation
                var resultsCopy = _results.ToList();
                var oldResults = resultsCopy.Where(r => r.RawResult.PluginID == resultId).ToList();
                // intersection of A (old results) and B (new newResults)
                var intersection = oldResults.Intersect(newResults).ToList();
                // remove result of relative complement of B in A
                foreach (var result in oldResults.Except(intersection))
                {
                    resultsCopy.Remove(result);
                }

                // update scores
                foreach (var result in newResults)
                {
                    if (IsTopMostResult(result.RawResult))
                    {
                        result.RawResult.Score = int.MaxValue;
                    }
                }

                // update index for result in intersection of A and B
                foreach (var commonResult in intersection)
                {
                    int oldIndex = resultsCopy.IndexOf(commonResult);
                    int oldScore = resultsCopy[oldIndex].RawResult.Score;
                    int newScore = newResults[newResults.IndexOf(commonResult)].RawResult.Score;
                    if (newScore != oldScore)
                    {
                        var oldResult = resultsCopy[oldIndex];
                        oldResult.RawResult.Score = newScore;
                        resultsCopy.RemoveAt(oldIndex);
                        int newIndex = InsertIndexOf(newScore, resultsCopy);
                        resultsCopy.Insert(newIndex, oldResult);

                    }
                }

                // insert result in relative complement of A in B
                foreach (var result in newResults.Except(intersection))
                {
                    int newIndex = InsertIndexOf(result.RawResult.Score, resultsCopy);
                    resultsCopy.Insert(newIndex, result);
                }

                // update UI in one run, so it can avoid UI flickering
                _results.Update(resultsCopy);

                if(this._results.Count > 0)
                {
                    this.Margin = new Thickness { Top = 8 };
                    this.SelectedResult = this._results[0];
                }
                else
                {
                    this.Margin = new Thickness { Top = 0 };
                }
            }
        }


        #endregion

        public class ResultCollection : ObservableCollection<ResultViewModel>
        {

            public ResultCollection()
            {
            }

            public void RemoveAll(Predicate<ResultViewModel> predicate)
            {
                CheckReentrancy();

                List<ResultViewModel> itemsToRemove = Items.Where(x => predicate(x)).ToList();
                if (itemsToRemove.Count > 0)
                {

                    itemsToRemove.ForEach(item => {

                        Items.Remove(item);

                    });

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
                    else if (oldResult.RawResult.Score != newResult.RawResult.Score)
                    {
                        this[i].RawResult.Score = newResult.RawResult.Score;
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
