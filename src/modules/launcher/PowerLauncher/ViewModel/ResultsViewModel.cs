// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using PowerLauncher.Helper;
using Wox.Infrastructure.UserSettings;
using Wox.Plugin;

namespace PowerLauncher.ViewModel
{
    public class ResultsViewModel : BaseModel
    {
        private readonly object _collectionLock = new object();

        private readonly PowerToysRunSettings _settings;
        private readonly IMainViewModel _mainViewModel;

        public ResultsViewModel()
        {
            Results = new ResultCollection();
            BindingOperations.EnableCollectionSynchronization(Results, _collectionLock);
        }

        public ResultsViewModel(PowerToysRunSettings settings, IMainViewModel mainViewModel)
            : this()
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _mainViewModel = mainViewModel;
            _settings.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(_settings.MaxResultsToShow))
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        OnPropertyChanged(nameof(MaxHeight));
                    });
                }
            };
        }

        public int MaxHeight
        {
            get
            {
                return _settings.MaxResultsToShow * 75;
            }
        }

        private int _selectedIndex;

        public int SelectedIndex
        {
            get => _selectedIndex;
            set
            {
                if (_selectedIndex != value)
                {
                    _selectedIndex = value;
                    OnPropertyChanged(nameof(SelectedIndex));
                }
            }
        }

        private ResultViewModel _selectedItem;

        public ResultViewModel SelectedItem
        {
            get
            {
                return _selectedItem;
            }

            set
            {
                if (value != null)
                {
                    if (_selectedItem != null)
                    {
                        _selectedItem.DeactivateContextButtons(ResultViewModel.ActivationType.Selection);
                    }

                    _selectedItem = value;
                    _selectedItem.ActivateContextButtons(ResultViewModel.ActivationType.Selection);
                }
                else
                {
                    _selectedItem = value;
                }
            }
        }

        private Visibility _visibility = Visibility.Hidden;

        public Visibility Visibility
        {
            get => _visibility;
            set
            {
                if (_visibility != value)
                {
                    _visibility = value;
                    OnPropertyChanged(nameof(Visibility));
                }
            }
        }

        public ResultCollection Results { get; }

        private static int InsertIndexOf(int newScore, IList<ResultViewModel> list)
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
            SelectedIndex = NewIndex(SelectedIndex + _settings.MaxResultsToShow);
        }

        public void SelectPrevPage()
        {
            SelectedIndex = NewIndex(SelectedIndex - _settings.MaxResultsToShow);
        }

        public void SelectFirstResult()
        {
            SelectedIndex = NewIndex(0);
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

        public void SelectNextTabItem()
        {
            if (_settings.TabSelectsContextButtons)
            {
                // Do nothing if there is no selected item or we've selected the next context button
                if (!SelectedItem?.SelectNextContextButton() ?? true)
                {
                    SelectNextResult();
                }
            }
            else
            {
                // Do nothing if there is no selected item
                if (SelectedItem != null)
                {
                    SelectNextResult();
                }
            }
        }

        public void SelectPrevTabItem()
        {
            if (_settings.TabSelectsContextButtons)
            {
                // Do nothing if there is no selected item or we've selected the previous context button
                if (!SelectedItem?.SelectPrevContextButton() ?? true)
                {
                    // Tabbing backwards should highlight the last item of the previous row
                    SelectPrevResult();
                    SelectedItem?.SelectLastContextButton();
                }
            }
            else
            {
                // Do nothing if there is no selected item
                if (SelectedItem != null)
                {
                    // Tabbing backwards should highlight the last item of the previous row
                    SelectPrevResult();
                    SelectedItem?.SelectLastContextButton();
                }
            }
        }

        public void SelectNextContextMenuItem()
        {
            if (SelectedItem != null)
            {
                if (!SelectedItem.SelectNextContextButton())
                {
                    SelectedItem.SelectLastContextButton();
                }
            }
        }

        public void SelectPreviousContextMenuItem()
        {
            if (SelectedItem != null)
            {
                SelectedItem.SelectPrevContextButton();
            }
        }

        public bool IsContextMenuItemSelected()
        {
            if (SelectedItem != null && SelectedItem.ContextMenuSelectedIndex != ResultViewModel.NoSelectionIndex)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// Add new results to ResultCollection
        /// </summary>
        public void AddResults(List<Result> newRawResults, CancellationToken ct)
        {
            if (newRawResults == null)
            {
                throw new ArgumentNullException(nameof(newRawResults));
            }

            List<ResultViewModel> newResults = new List<ResultViewModel>(newRawResults.Count);
            foreach (Result r in newRawResults)
            {
                newResults.Add(new ResultViewModel(r, _mainViewModel));
                ct.ThrowIfCancellationRequested();
            }

            Results.AddRange(newResults);
        }

        public void Sort(MainViewModel.QueryTuningOptions options)
        {
            List<ResultViewModel> sorted = null;

            if (options.SearchQueryTuningEnabled)
            {
                sorted = Results.OrderByDescending(x => (x.Result.Metadata.WeightBoost + x.Result.Score + (x.Result.SelectedCount * options.SearchClickedItemWeight))).ToList();
            }
            else
            {
                sorted = Results.OrderByDescending(x => (x.Result.Metadata.WeightBoost + x.Result.Score + (x.Result.SelectedCount * 5))).ToList();
            }

            // remove history items in they are in the list as non-history items
            foreach (var nonHistoryResult in sorted.Where(x => x.Result.Metadata.Name != "History").ToList())
            {
                var historyToRemove = sorted.FirstOrDefault(x => x.Result.Metadata.Name == "History" && x.Result.Title == nonHistoryResult.Result.Title && x.Result.SubTitle == nonHistoryResult.Result.SubTitle);
                if (historyToRemove != null)
                {
                    sorted.Remove(historyToRemove);
                }
            }

            Clear();
            Results.AddRange(sorted);
        }

        public static readonly DependencyProperty FormattedTextProperty = DependencyProperty.RegisterAttached(
            "FormattedText",
            typeof(Inline),
            typeof(ResultsViewModel),
            new PropertyMetadata(null, FormattedTextPropertyChanged));

        public static void SetFormattedText(DependencyObject textBlock, IList<int> value)
        {
            if (textBlock != null)
            {
                textBlock.SetValue(FormattedTextProperty, value);
            }
        }

        public static Inline GetFormattedText(DependencyObject textBlock)
        {
            return (Inline)textBlock?.GetValue(FormattedTextProperty);
        }

        private static void FormattedTextPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var textBlock = d as TextBlock;
            if (textBlock == null)
            {
                return;
            }

            var inline = (Inline)e.NewValue;

            textBlock.Inlines.Clear();
            if (inline == null)
            {
                return;
            }

            textBlock.Inlines.Add(inline);
        }
    }
}
