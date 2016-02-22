using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Wox.Core.Plugin;
using Wox.Core.Resource;
using Wox.Infrastructure;
using Wox.Plugin;
using Wox.Storage;
using Stopwatch = Wox.Infrastructure.Stopwatch;

namespace Wox.ViewModel
{
    public class MainViewModel : BaseViewModel
    {
        #region Private Fields

        private string _queryText;
        private bool _isVisible;
        private bool _isResultListBoxVisible;
        private bool _isContextMenuVisible;
        private bool _isProgressBarVisible;
        private bool _isProgressBarTooltipVisible;
        private bool _selectAllText;
        private int _caretIndex;
        private double _left;
        private double _top;

        private bool _queryHasReturn;
        private Query _lastQuery = new Query();
        private bool _ignoreTextChange;
        private List<Result> _currentContextMenus = new List<Result>();
        private string _textBeforeEnterContextMenuMode;

        #endregion

        #region Constructor

        public MainViewModel()
        {
            InitializeResultListBox();
            InitializeContextMenu();
            InitializeKeyCommands();

            _queryHasReturn = false;
        }

        #endregion

        #region ViewModel Properties

        public ResultsViewModel Results { get; private set; }

        public ResultsViewModel ContextMenu { get; private set; }

        public string QueryText
        {
            get
            {
                return _queryText;
            }
            set
            {
                _queryText = value;
                OnPropertyChanged("QueryText");

                HandleQueryTextUpdated();
            }
        }

        public bool SelectAllText
        {
            get
            {
                return _selectAllText;
            }
            set
            {
                _selectAllText = value;
                OnPropertyChanged("SelectAllText");
            }
        }

        public int CaretIndex
        {
            get
            {
                return _caretIndex;
            }
            set
            {
                _caretIndex = value;
                OnPropertyChanged("CaretIndex");
            }
        }

        public bool IsVisible
        {
            get
            {
                return _isVisible;
            }
            set
            {
                _isVisible = value;
                OnPropertyChanged("IsVisible");

                if (!value && IsContextMenuVisible)
                {
                    BackToSearchMode();
                }
            }
        }

        public bool IsResultListBoxVisible
        {
            get
            {
                return _isResultListBoxVisible;
            }
            set
            {
                _isResultListBoxVisible = value;
                OnPropertyChanged("IsResultListBoxVisible");
            }
        }

        public bool IsContextMenuVisible
        {
            get
            {
                return _isContextMenuVisible;
            }
            set
            {
                _isContextMenuVisible = value;
                OnPropertyChanged("IsContextMenuVisible");
            }
        }

        public bool IsProgressBarVisible
        {
            get
            {
                return _isProgressBarVisible;
            }
            set
            {
                _isProgressBarVisible = value;
                OnPropertyChanged("IsProgressBarVisible");
            }
        }

        public bool IsProgressBarTooltipVisible
        {
            get
            {
                return _isProgressBarTooltipVisible;
            }
            set
            {
                _isProgressBarTooltipVisible = value;
                OnPropertyChanged("IsProgressBarTooltipVisible");
            }
        }

        public double Left
        {
            get
            {
                return _left;
            }
            set
            {
                _left = value;
                OnPropertyChanged("Left");
            }
        }

        public double Top
        {
            get
            {
                return _top;
            }
            set
            {
                _top = value;
                OnPropertyChanged("Top");
            }
        }

        public ICommand EscCommand { get; set; }
        public ICommand SelectNextItemCommand { get; set; }
        public ICommand SelectPrevItemCommand { get; set; }
        public ICommand CtrlOCommand { get; set; }
        public ICommand DisplayNextQueryCommand { get; set; }
        public ICommand DisplayPrevQueryCommand { get; set; }
        public ICommand SelectNextPageCommand { get; set; }
        public ICommand SelectPrevPageCommand { get; set; }
        public ICommand StartHelpCommand { get; set; }
        public ICommand ShiftEnterCommand { get; set; }
        public ICommand OpenResultCommand { get; set; }
        public ICommand BackCommand { get; set; }
        #endregion

        #region Private Methods

        private void InitializeKeyCommands()
        {
            EscCommand = new RelayCommand(_ =>
            {
                if (IsContextMenuVisible)
                {
                    BackToSearchMode();
                }
                else
                {
                    IsVisible = false;
                }
            });

            SelectNextItemCommand = new RelayCommand(o =>
            {
                if (IsContextMenuVisible)
                {
                    ContextMenu.SelectNextResult();
                }
                else
                {
                    Results.SelectNextResult();
                }
            });

            SelectPrevItemCommand = new RelayCommand(_ =>
            {
                if (IsContextMenuVisible)
                {
                    ContextMenu.SelectPrevResult();
                }
                else
                {
                    Results.SelectPrevResult();
                }
            });

            CtrlOCommand = new RelayCommand(_ =>
            {
                if (IsContextMenuVisible)
                {
                    BackToSearchMode();
                }
                else
                {
                    ShowContextMenu(Results.SelectedResult.RawResult);
                }
            });

            DisplayNextQueryCommand = new RelayCommand(_ =>
            {
                var nextQuery = QueryHistoryStorage.Instance.Next();
                DisplayQueryHistory(nextQuery);
            });

            DisplayPrevQueryCommand = new RelayCommand(_ =>
            {
                var prev = QueryHistoryStorage.Instance.Previous();
                DisplayQueryHistory(prev);
            });

            SelectNextPageCommand = new RelayCommand(_ =>
            {
                Results.SelectNextPage();
            });

            SelectPrevPageCommand = new RelayCommand(_ =>
            {
                Results.SelectPrevPage();
            });

            StartHelpCommand = new RelayCommand(_ =>
            {
                Process.Start("http://doc.getwox.com");
            });

            ShiftEnterCommand = new RelayCommand(_ =>
            {
                if (!IsContextMenuVisible && Results.SelectedResult != null)
                {
                    ShowContextMenu(Results.SelectedResult.RawResult);
                }
            });

            OpenResultCommand = new RelayCommand(o =>
            {
                if (o != null)
                {
                    var index = int.Parse(o.ToString());
                    Results.SelectResult(index);
                }
                Results.SelectedResult?.OpenResultListBoxItemCommand.Execute(null);
            });

            BackCommand = new RelayCommand(_ =>
            {
                ListeningKeyPressed?.Invoke(this, new ListeningKeyPressedEventArgs(_ as KeyEventArgs));
            });
        }

        private void InitializeResultListBox()
        {
            Results = new ResultsViewModel();
            IsResultListBoxVisible = false;
        }

        private void ShowContextMenu(Result result)
        {
            if (result == null) return;
            ShowContextMenu(result, PluginManager.GetContextMenusForPlugin(result));
        }

        private void ShowContextMenu(Result result, List<Result> actions)
        {
            actions.ForEach(o =>
            {
                o.PluginDirectory = PluginManager.GetPluginForId(result.PluginID).Metadata.PluginDirectory;
                o.PluginID = result.PluginID;
                o.OriginQuery = result.OriginQuery;
            });

            actions.Add(GetTopMostContextMenu(result));

            DisplayContextMenu(actions, result.PluginID);
        }

        private void DisplayContextMenu(List<Result> actions, string pluginID)
        {
            _textBeforeEnterContextMenuMode = QueryText;

            ContextMenu.Clear();
            ContextMenu.AddResults(actions, pluginID);
            _currentContextMenus = actions;

            IsContextMenuVisible = true;
            IsResultListBoxVisible = false;

            QueryText = "";
        }

        private Result GetTopMostContextMenu(Result result)
        {
            if (TopMostRecordStorage.Instance.IsTopMost(result))
            {
                return new Result(InternationalizationManager.Instance.GetTranslation("cancelTopMostInThisQuery"), "Images\\down.png")
                {
                    PluginDirectory = WoxDirectroy.Executable,
                    Action = _ =>
                    {
                        TopMostRecordStorage.Instance.Remove(result);
                        App.API.ShowMsg("Succeed", "", "");
                        return false;
                    }
                };
            }
            else
            {
                return new Result(InternationalizationManager.Instance.GetTranslation("setAsTopMostInThisQuery"), "Images\\up.png")
                {
                    PluginDirectory = WoxDirectroy.Executable,
                    Action = _ =>
                    {
                        TopMostRecordStorage.Instance.AddOrUpdate(result);
                        App.API.ShowMsg("Succeed", "", "");
                        return false;
                    }
                };
            }
        }

        private void InitializeContextMenu()
        {
            ContextMenu = new ResultsViewModel();
            IsContextMenuVisible = false;
        }

        private void HandleQueryTextUpdated()
        {
            if (_ignoreTextChange) { _ignoreTextChange = false; return; }

            IsProgressBarTooltipVisible = false;
            if (IsContextMenuVisible)
            {
                QueryContextMenu();
            }
            else
            {
                string query = QueryText.Trim();
                if (!string.IsNullOrEmpty(query))
                {
                    Query(query);
                    //reset query history index after user start new query
                    ResetQueryHistoryIndex();
                }
                else
                {
                    Results.Clear();
                }
            }
        }

        private void QueryContextMenu()
        {
            var contextMenuId = "Context Menu Id";
            ContextMenu.Clear();
            var query = QueryText.ToLower();
            if (string.IsNullOrEmpty(query))
            {
                ContextMenu.AddResults(_currentContextMenus, contextMenuId);
            }
            else
            {
                List<Result> filterResults = new List<Result>();
                foreach (Result contextMenu in _currentContextMenus)
                {
                    if (StringMatcher.IsMatch(contextMenu.Title, query)
                        || StringMatcher.IsMatch(contextMenu.SubTitle, query))
                    {
                        filterResults.Add(contextMenu);
                    }
                }
                ContextMenu.AddResults(filterResults, contextMenuId);
            }
        }

        private void Query(string text)
        {
            _queryHasReturn = false;
            var query = PluginManager.QueryInit(text);
            if (query != null)
            {
                // handle the exclusiveness of plugin using action keyword
                string lastKeyword = _lastQuery.ActionKeyword;
                string keyword = query.ActionKeyword;
                if (string.IsNullOrEmpty(lastKeyword))
                {
                    if (!string.IsNullOrEmpty(keyword))
                    {
                        Results.RemoveResultsExcept(PluginManager.NonGlobalPlugins[keyword].Metadata);
                    }
                }
                else
                {
                    if (string.IsNullOrEmpty(keyword))
                    {
                        Results.RemoveResultsFor(PluginManager.NonGlobalPlugins[lastKeyword].Metadata);
                    }
                    else if (lastKeyword != keyword)
                    {
                        Results.RemoveResultsExcept(PluginManager.NonGlobalPlugins[keyword].Metadata);
                    }
                }
                _lastQuery = query;

                Action action = async () =>
                {
                    await Task.Delay(150);
                    if (!string.IsNullOrEmpty(query.RawQuery) && query.RawQuery == _lastQuery.RawQuery && !_queryHasReturn)
                    {
                        IsProgressBarTooltipVisible = true;
                    }
                };
                action.Invoke();

                //Application.Current.Dispatcher.InvokeAsync(async () =>
                //{
                //    await Task.Delay(150);
                //    if (!string.IsNullOrEmpty(query.RawQuery) && query.RawQuery == _lastQuery.RawQuery && !_queryHasReturn)
                //    {
                //        StartProgress();
                //    }
                //});
                PluginManager.QueryForAllPlugins(query);
            }

            IsProgressBarTooltipVisible = false;
        }

        private void ResetQueryHistoryIndex()
        {
            Results.RemoveResultsFor(QueryHistoryStorage.MetaData);
            QueryHistoryStorage.Instance.Reset();
        }

        private void UpdateResultViewInternal(List<Result> list, PluginMetadata metadata)
        {
            Stopwatch.Normal($"UI update cost for {metadata.Name}",
                    () => { Results.AddResults(list, metadata.ID); });
        }

        private void BackToSearchMode()
        {
            QueryText = _textBeforeEnterContextMenuMode;
            IsContextMenuVisible = false;
            IsResultListBoxVisible = true;
            CaretIndex = QueryText.Length;
        }

        private void DisplayQueryHistory(HistoryItem history)
        {
            if (history != null)
            {
                var historyMetadata = QueryHistoryStorage.MetaData;

                QueryText = history.Query;
                SelectAllText = true;

                var executeQueryHistoryTitle = InternationalizationManager.Instance.GetTranslation("executeQuery");
                var lastExecuteTime = InternationalizationManager.Instance.GetTranslation("lastExecuteTime");
                Results.RemoveResultsExcept(historyMetadata);
                UpdateResultViewInternal(new List<Result>
                {
                    new Result
                    {
                        Title = string.Format(executeQueryHistoryTitle,history.Query),
                        SubTitle = string.Format(lastExecuteTime,history.ExecutedDateTime),
                        IcoPath = "Images\\history.png",
                        PluginDirectory = WoxDirectroy.Executable,
                        Action = _ =>{

                            QueryText = history.Query;
                            SelectAllText = true;

                            return false;
                        }
                    }
                }, historyMetadata);
            }
        }

        #endregion

        #region Public Methods

        public void UpdateResultView(List<Result> list, PluginMetadata metadata, Query originQuery)
        {
            _queryHasReturn = true;
            IsProgressBarTooltipVisible = false;

            list.ForEach(o =>
            {
                o.Score += UserSelectedRecordStorage.Instance.GetSelectedCount(o) * 5;
            });
            if (originQuery.RawQuery == _lastQuery.RawQuery)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    UpdateResultViewInternal(list, metadata);
                });
            }

            if (list.Count > 0)
            {
                IsResultListBoxVisible = true;
            }
        }

        public void ShowContextMenu(List<Result> actions, string pluginID)
        {
            DisplayContextMenu(actions, pluginID);
        }

        #endregion

        public event EventHandler<ListeningKeyPressedEventArgs> ListeningKeyPressed;

    }

    public class ListeningKeyPressedEventArgs : EventArgs
    {

        public KeyEventArgs KeyEventArgs
        {
            get;
            private set;
        }

        public ListeningKeyPressedEventArgs(KeyEventArgs keyEventArgs)
        {
            KeyEventArgs = keyEventArgs;
        }

    }
}
