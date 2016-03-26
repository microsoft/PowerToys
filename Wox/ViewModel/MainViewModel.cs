using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Wox.Core.Plugin;
using Wox.Core.Resource;
using Wox.Helper;
using Wox.Infrastructure;
using Wox.Infrastructure.Hotkey;
using Wox.Plugin;
using Wox.Storage;
using Stopwatch = Wox.Infrastructure.Stopwatch;

namespace Wox.ViewModel
{
    public class MainViewModel : BaseViewModel
    {
        #region Private Fields

        private bool _isProgressBarTooltipVisible;
        private double _left;
        private double _top;

        private Visibility _contextMenuVisibility;
        private Visibility _progressBarVisibility;
        private Visibility _resultListBoxVisibility;
        private Visibility _mainWindowVisibility;

        private bool _queryHasReturn;
        private Query _lastQuery;
        private bool _ignoreTextChange;
        private string _queryTextBeforeLoadContextMenu;
        private string _queryText;

        #endregion

        #region Constructor

        public MainViewModel()
        {
            _queryTextBeforeLoadContextMenu = "";
            _queryText = "";
            _lastQuery = new Query();

            InitializeResultListBox();
            InitializeContextMenu();
            InitializeKeyCommands();
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
                OnPropertyChanged();

                if (_ignoreTextChange)
                {
                    _ignoreTextChange = false;
                }
                else
                {
                    HandleQueryTextUpdated();
                }
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
                OnPropertyChanged();
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
                OnPropertyChanged();
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
                OnPropertyChanged();
            }
        }

        public Visibility ContextMenuVisibility

        {
            get
            {
                return _contextMenuVisibility;
            }
            set
            {
                _contextMenuVisibility = value;
                OnPropertyChanged();

                _ignoreTextChange = true;
                if (!value.IsVisible())
                {
                    QueryText = _queryTextBeforeLoadContextMenu;
                    ResultListBoxVisibility = Visibility.Visible;
                    OnCursorMovedToEnd();
                }
                else
                {
                    _queryTextBeforeLoadContextMenu = QueryText;
                    QueryText = "";
                    ResultListBoxVisibility = Visibility.Collapsed;
                }
            }
        }

        public Visibility ProgressBarVisibility
        {
            get
            {
                return _progressBarVisibility;
            }
            set
            {
                _progressBarVisibility = value;
                OnPropertyChanged();
            }
        }

        public Visibility ResultListBoxVisibility
        {
            get
            {
                return _resultListBoxVisibility;
            }
            set
            {
                _resultListBoxVisibility = value;
                OnPropertyChanged();
            }
        }

        public Visibility MainWindowVisibility
        {
            get
            {
                return _mainWindowVisibility;
            }
            set
            {
                _mainWindowVisibility = value;
                OnPropertyChanged();
                MainWindowVisibilityChanged?.Invoke(this, new EventArgs());
            }
        }

        public ICommand EscCommand { get; set; }
        public ICommand SelectNextItemCommand { get; set; }
        public ICommand SelectPrevItemCommand { get; set; }
        public ICommand DisplayNextQueryCommand { get; set; }
        public ICommand DisplayPrevQueryCommand { get; set; }
        public ICommand SelectNextPageCommand { get; set; }
        public ICommand SelectPrevPageCommand { get; set; }
        public ICommand StartHelpCommand { get; set; }
        public ICommand LoadContextMenuCommand { get; set; }
        public ICommand OpenResultCommand { get; set; }
        public ICommand BackCommand { get; set; }
        #endregion

        #region Private Methods

        private void InitializeKeyCommands()
        {
            EscCommand = new RelayCommand(_ =>
            {
                if (ContextMenuVisibility.IsVisible())
                {
                    ContextMenuVisibility = Visibility.Collapsed;
                }
                else
                {
                    MainWindowVisibility = Visibility.Collapsed;
                }
            });

            SelectNextItemCommand = new RelayCommand(_ =>
            {
                if (ContextMenuVisibility.IsVisible())
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
                if (ContextMenuVisibility.IsVisible())
                {
                    ContextMenu.SelectPrevResult();
                }
                else
                {
                    Results.SelectPrevResult();
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

            OpenResultCommand = new RelayCommand(o =>
            {
                var results = ContextMenuVisibility.IsVisible() ? ContextMenu : Results;

                if (o != null)
                {
                    var index = int.Parse(o.ToString());
                    results.SelectResult(index);
                }

                var result = results.SelectedResult.RawResult;
                bool hideWindow = result.Action(new ActionContext
                {
                    SpecialKeyState = GlobalHotkey.Instance.CheckModifiers()
                });
                if (hideWindow)
                {
                    MainWindowVisibility = Visibility.Collapsed;
                }

                UserSelectedRecordStorage.Instance.Add(result);
                QueryHistoryStorage.Instance.Add(result.OriginQuery.RawQuery);
            });

            LoadContextMenuCommand = new RelayCommand(_ =>
            {
                if (!ContextMenuVisibility.IsVisible())
                {
                    var result = Results.SelectedResult.RawResult;
                    var pluginID = result.PluginID;

                    var contextMenuResults = PluginManager.GetContextMenusForPlugin(result);
                    contextMenuResults.Add(GetTopMostContextMenu(result));

                    ContextMenu.Clear();
                    ContextMenu.AddResults(contextMenuResults, pluginID);
                    ContextMenuVisibility = Visibility.Visible;
                }
                else
                {
                    ContextMenuVisibility = Visibility.Collapsed;
                }
            });

            BackCommand = new RelayCommand(_ =>
            {
                ListeningKeyPressed?.Invoke(this, new ListeningKeyPressedEventArgs(_ as KeyEventArgs));
            });
        }

        private void InitializeResultListBox()
        {
            Results = new ResultsViewModel();
            ResultListBoxVisibility = Visibility.Collapsed;
        }


        private void InitializeContextMenu()
        {
            ContextMenu = new ResultsViewModel();
            ContextMenuVisibility = Visibility.Collapsed;
        }

        private void HandleQueryTextUpdated()
        {
            IsProgressBarTooltipVisible = false;
            if (ContextMenuVisibility.IsVisible())
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
            var query = QueryText.ToLower();
            if (!string.IsNullOrEmpty(query))
            {

                List<Result> filterResults = new List<Result>();
                foreach (var contextMenu in ContextMenu.Results)
                {
                    if (StringMatcher.IsMatch(contextMenu.Title, query)
                        || StringMatcher.IsMatch(contextMenu.SubTitle, query))
                    {
                        filterResults.Add(contextMenu.RawResult);
                    }
                }
                ContextMenu.Clear();
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

        private void DisplayQueryHistory(HistoryItem history)
        {
            if (history != null)
            {
                var historyMetadata = QueryHistoryStorage.MetaData;

                QueryText = history.Query;
                OnTextBoxSelected();

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
                            OnTextBoxSelected();
                            return false;
                        }
                    }
                }, historyMetadata);
            }
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
                        App.API.ShowMsg("Succeed");
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
                        App.API.ShowMsg("Succeed");
                        return false;
                    }
                };
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
                ResultListBoxVisibility = Visibility.Visible;
            }
        }

        #endregion

        public event EventHandler<ListeningKeyPressedEventArgs> ListeningKeyPressed;
        public event EventHandler MainWindowVisibilityChanged;

        public event EventHandler CursorMovedToEnd;
        public void OnCursorMovedToEnd()
        {
            CursorMovedToEnd?.Invoke(this, new EventArgs());
        }

        public event EventHandler TextBoxSelected;
        public void OnTextBoxSelected()
        {
            TextBoxSelected?.Invoke(this, new EventArgs());
        }
    }

    public class ListeningKeyPressedEventArgs : EventArgs
    {
        public KeyEventArgs KeyEventArgs { get; private set; }

        public ListeningKeyPressedEventArgs(KeyEventArgs keyEventArgs)
        {
            KeyEventArgs = keyEventArgs;
        }
    }
}
