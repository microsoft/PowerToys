using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Data;
using Wox.Infrastructure.UserSettings;
using Wox.Plugin;

namespace Wox.ViewModel
{
    public class ResultsViewModel : BaseModel
    {
        #region Private Fields

        public ResultCollection Results { get; }

        private readonly object _addResultsLock = new object();
        private readonly object _collectionLock = new object();
        private readonly Settings _settings;
        private int MaxResults => _settings?.MaxResultsToShow ?? 6;

        public ResultsViewModel()
        {
            Results = new ResultCollection();
            BindingOperations.EnableCollectionSynchronization(Results, _collectionLock);
        }
        public ResultsViewModel(Settings settings) : this()
        {
            _settings = settings;
            _settings.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(_settings.MaxResultsToShow))
                {
                    OnPropertyChanged(nameof(MaxHeight));
                }
            };
        }

        #endregion

        #region Properties

        public int MaxHeight => MaxResults * 50;

        public int SelectedIndex { get; set; }

        public ResultViewModel SelectedItem { get; set; }
        public Thickness Margin { get; set; }
        public Visibility Visbility { get; set; } = Visibility.Collapsed;

        #endregion

        #region Private Methods

        private int InsertIndexOf(int newScore, IList<ResultViewModel> list)
        {
            int index = 0;
            for (; index < list.Count; index++)
            {
                var result = list[index];
                if (newScore > result.Result.Score)
                {
                    break;
                }
            }
            return index;
        }

        private int NewIndex(int i)
        {
            var n = Results.Count;
            if (n > 0)
            {
                i = (n + i) % n;
                return i;
            }
            else
            {
                // SelectedIndex returns -1 if selection is empty.
                return -1;
            }
        }


        #endregion

        #region Public Methods

        public void SelectNextResult()
        {
            SelectedIndex = NewIndex(SelectedIndex + 1);
        }

        public void SelectPrevResult()
        {
            SelectedIndex = NewIndex(SelectedIndex - 1);
        }

        public void SelectNextPage()
        {
            SelectedIndex = NewIndex(SelectedIndex + MaxResults);
        }

        public void SelectPrevPage()
        {
            SelectedIndex = NewIndex(SelectedIndex - MaxResults);
        }

        public void Clear()
        {
            Results.Clear();
        }

        public void RemoveResultsExcept(PluginMetadata metadata)
        {
            Results.RemoveAll(r => r.Result.PluginID != metadata.ID);
        }

        public void RemoveResultsFor(PluginMetadata metadata)
        {
            Results.RemoveAll(r => r.Result.PluginID == metadata.ID);
        }

        /// <summary>
        /// To avoid deadlock, this method should not called from main thread
        /// </summary>
        public void AddResults(List<Result> newRawResults, string resultId)
        {
            lock (_addResultsLock)
            {
                var newResults = NewResults(newRawResults, resultId);

                // update UI in one run, so it can avoid UI flickering
                Results.Update(newResults);

                if (Results.Count > 0)
                {
                    Margin = new Thickness { Top = 8 };
                    SelectedIndex = 0;
                }
                else
                {
                    Margin = new Thickness { Top = 0 };
                }
            }
        }

        private List<ResultViewModel> NewResults(List<Result> newRawResults, string resultId)
        {
            var newResults = newRawResults.Select(r => new ResultViewModel(r)).ToList();
            var results = Results.ToList();
            var oldResults = results.Where(r => r.Result.PluginID == resultId).ToList();

            // intersection of A (old results) and B (new newResults)
            var intersection = oldResults.Intersect(newResults).ToList();

            // remove result of relative complement of B in A
            foreach (var result in oldResults.Except(intersection))
            {
                results.Remove(result);
            }

            // update index for result in intersection of A and B
            foreach (var commonResult in intersection)
            {
                int oldIndex = results.IndexOf(commonResult);
                int oldScore = results[oldIndex].Result.Score;
                var newResult = newResults[newResults.IndexOf(commonResult)];
                int newScore = newResult.Result.Score;
                if (newScore != oldScore)
                {
                    var oldResult = results[oldIndex];

                    oldResult.Result.Score = newScore;
                    oldResult.Result.OriginQuery = newResult.Result.OriginQuery;

                    results.RemoveAt(oldIndex);
                    int newIndex = InsertIndexOf(newScore, results);
                    results.Insert(newIndex, oldResult);
                }
            }

            // insert result in relative complement of A in B
            foreach (var result in newResults.Except(intersection))
            {
                int newIndex = InsertIndexOf(result.Result.Score, results);
                results.Insert(newIndex, result);
            }

            return results;
        }


        #endregion

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

            public void Update(List<ResultViewModel> newItems)
            {
                int newCount = newItems.Count;
                int oldCount = Items.Count;
                int location = newCount > oldCount ? oldCount : newCount;

                for (int i = 0; i < location; i++)
                {
                    ResultViewModel oldResult = this[i];
                    ResultViewModel newResult = newItems[i];
                    if (!oldResult.Equals(newResult))
                    {
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
}
