using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Input;
using Wox.Core.Plugin;
using Wox.Core.Resource;
using Wox.Core.UserSettings;
using Wox.Infrastructure;
using Wox.Infrastructure.Hotkey;
using Wox.Plugin;
using Wox.Storage;

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
        private List<Result> CurrentContextMenus = new List<Result>();
        private string _textBeforeEnterContextMenuMode;

        #endregion

        #region Constructor

        public MainViewModel()
        {
            this.InitializeResultListBox();
            this.InitializeContextMenu();
            this.InitializeKeyCommands();

            this._queryHasReturn = false;
        }

        #endregion

        #region ViewModel Properties

        public ResultsViewModel Results { get; private set; }

        public ResultsViewModel ContextMenu { get; private set; }

        public string QueryText
        {
            get
            {
                return this._queryText;
            }
            set
            {
                this._queryText = value;
                OnPropertyChanged("QueryText");

                this.HandleQueryTextUpdated();
            }
        }

        public bool SelectAllText
        {
            get
            {
                return this._selectAllText;
            }
            set
            {
                this._selectAllText = value;
                OnPropertyChanged("SelectAllText");
            }
        }

        public int CaretIndex
        {
            get
            {
                return this._caretIndex;
            }
            set
            {
                this._caretIndex = value;
                OnPropertyChanged("CaretIndex");
            }
        }

        public bool IsVisible
        {
            get
            {
                return this._isVisible;
            }
            set
            {
                this._isVisible = value;
                OnPropertyChanged("IsVisible");

                if (!value && this.IsContextMenuVisible)
                {
                    this.BackToSearchMode();
                }
            }
        }

        public bool IsResultListBoxVisible
        {
            get
            {
                return this._isResultListBoxVisible;
            }
            set
            {
                this._isResultListBoxVisible = value;
                OnPropertyChanged("IsResultListBoxVisible");
            }
        }

        public bool IsContextMenuVisible
        {
            get
            {
                return this._isContextMenuVisible;
            }
            set
            {
                this._isContextMenuVisible = value;
                OnPropertyChanged("IsContextMenuVisible");
            }
        }

        public bool IsProgressBarVisible
        {
            get
            {
                return this._isProgressBarVisible;
            }
            set
            {
                this._isProgressBarVisible = value;
                OnPropertyChanged("IsProgressBarVisible");
            }
        }

        public bool IsProgressBarTooltipVisible
        {
            get
            {
                return this._isProgressBarTooltipVisible;
            }
            set
            {
                this._isProgressBarTooltipVisible = value;
                OnPropertyChanged("IsProgressBarTooltipVisible");
            }
        }

        public double Left
        {
            get
            {
                return this._left;
            }
            set
            {
                this._left = value;
                OnPropertyChanged("Left");
            }
        }

        public double Top
        {
            get
            {
                return this._top;
            }
            set
            {
                this._top = value;
                OnPropertyChanged("Top");
            }
        }

        public ICommand EscCommand
        {
            get;
            set;
        }

        public ICommand SelectNextItemCommand
        {
            get;
            set;
        }

        public ICommand SelectPrevItemCommand
        {
            get;
            set;
        }

        public ICommand CtrlOCommand
        {
            get;
            set;
        }

        public ICommand DisplayNextQueryCommand
        {
            get;
            set;
        }

        public ICommand DisplayPrevQueryCommand
        {
            get;
            set;
        }

        public ICommand SelectNextPageCommand
        {
            get;
            set;
        }

        public ICommand SelectPrevPageCommand
        {
            get;
            set;
        }

        public ICommand StartHelpCommand
        {
            get;
            set;
        }

        public ICommand ShiftEnterCommand
        {
            get;
            set;
        }

        public ICommand OpenResultCommand
        {
            get;
            set;
        }

        public ICommand BackCommand
        {
            get;
            set;
        }

        #endregion

        #region Private Methods

        private void InitializeKeyCommands()
        {
            this.EscCommand = new RelayCommand((parameter) =>
            {

                if (this.IsContextMenuVisible)
                {
                    this.BackToSearchMode();
                }
                else
                {
                    this.IsVisible = false;
                }

            });

            this.SelectNextItemCommand = new RelayCommand((parameter) =>
            {

                if (this.IsContextMenuVisible)
                {
                    this.ContextMenu.SelectNextResult();
                }
                else
                {
                    this.Results.SelectNextResult();
                }

            });

            this.SelectPrevItemCommand = new RelayCommand((parameter) =>
            {

                if (this.IsContextMenuVisible)
                {
                    this.ContextMenu.SelectPrevResult();
                }
                else
                {
                    this.Results.SelectPrevResult();
                }

            });

            this.CtrlOCommand = new RelayCommand((parameter) =>
            {

                if (this.IsContextMenuVisible)
                {
                    BackToSearchMode();
                }
                else
                {
                    ShowContextMenu(this.Results.SelectedResult.RawResult);
                }
            });

            this.DisplayNextQueryCommand = new RelayCommand((parameter) =>
            {

                var nextQuery = QueryHistoryStorage.Instance.Next();
                DisplayQueryHistory(nextQuery);

            });

            this.DisplayPrevQueryCommand = new RelayCommand((parameter) =>
            {

                var prev = QueryHistoryStorage.Instance.Previous();
                DisplayQueryHistory(prev);

            });

            this.SelectNextPageCommand = new RelayCommand((parameter) =>
            {

                this.Results.SelectNextPage();

            });

            this.SelectPrevPageCommand = new RelayCommand((parameter) =>
            {

                this.Results.SelectPrevPage();

            });

            this.StartHelpCommand = new RelayCommand((parameter) =>
            {
                Process.Start("http://doc.getwox.com");
            });

            this.ShiftEnterCommand = new RelayCommand((parameter) =>
            {

                if (!this.IsContextMenuVisible && null != this.Results.SelectedResult)
                {
                    this.ShowContextMenu(this.Results.SelectedResult.RawResult);
                }

            });

            this.OpenResultCommand = new RelayCommand((parameter) =>
            {

                if (null != parameter)
                {
                    var index = int.Parse(parameter.ToString());
                    this.Results.SelectResult(index);
                }

                if (null != this.Results.SelectedResult)
                {
                    this.Results.SelectedResult.OpenResultListBoxItemCommand.Execute(null);
                }
            });

            this.BackCommand = new RelayCommand((parameter) =>
            {
                if (null != ListeningKeyPressed)
                {
                    this.ListeningKeyPressed(this, new ListeningKeyPressedEventArgs(parameter as System.Windows.Input.KeyEventArgs));
                }

            });
        }

        private void InitializeResultListBox()
        {
            this.Results = new ResultsViewModel();
            this.IsResultListBoxVisible = false;
        }

        private void ShowContextMenu(Result result)
        {
            if (result == null) return;
            this.ShowContextMenu(result, PluginManager.GetContextMenusForPlugin(result));
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

            this.DisplayContextMenu(actions, result.PluginID);
        }

        private void DisplayContextMenu(List<Result> actions, string pluginID)
        {
            _textBeforeEnterContextMenuMode = this.QueryText;

            this.ContextMenu.Clear();
            this.ContextMenu.AddResults(actions, pluginID);
            CurrentContextMenus = actions;

            this.IsContextMenuVisible = true;
            this.IsResultListBoxVisible = false;

            this.QueryText = "";
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
            this.ContextMenu = new ResultsViewModel();
            this.IsContextMenuVisible = false;
        }

        private void HandleQueryTextUpdated()
        {
            if (_ignoreTextChange) { _ignoreTextChange = false; return; }

            this.IsProgressBarTooltipVisible = false;
            if (this.IsContextMenuVisible)
            {
                QueryContextMenu();
            }
            else
            {
                string query = this.QueryText.Trim();
                if (!string.IsNullOrEmpty(query))
                {
                    Query(query);
                    //reset query history index after user start new query
                    ResetQueryHistoryIndex();
                }
                else
                {
                    this.Results.Clear();
                }
            }
        }

        private void QueryContextMenu()
        {
            var contextMenuId = "Context Menu Id";
            this.ContextMenu.Clear();
            var query = this.QueryText.ToLower();
            if (string.IsNullOrEmpty(query))
            {
                this.ContextMenu.AddResults(CurrentContextMenus, contextMenuId);
            }
            else
            {
                List<Result> filterResults = new List<Result>();
                foreach (Result contextMenu in CurrentContextMenus)
                {
                    if (StringMatcher.IsMatch(contextMenu.Title, query)
                        || StringMatcher.IsMatch(contextMenu.SubTitle, query))
                    {
                        filterResults.Add(contextMenu);
                    }
                }
                this.ContextMenu.AddResults(filterResults, contextMenuId);
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
                        this.Results.RemoveResultsExcept(PluginManager.NonGlobalPlugins[keyword].Metadata);
                    }
                }
                else
                {
                    if (string.IsNullOrEmpty(keyword))
                    {
                        this.Results.RemoveResultsFor(PluginManager.NonGlobalPlugins[lastKeyword].Metadata);
                    }
                    else if (lastKeyword != keyword)
                    {
                        this.Results.RemoveResultsExcept(PluginManager.NonGlobalPlugins[keyword].Metadata);
                    }
                }
                _lastQuery = query;

                Action action = new Action(async () =>
                {
                    await Task.Delay(150);
                    if (!string.IsNullOrEmpty(query.RawQuery) && query.RawQuery == _lastQuery.RawQuery && !_queryHasReturn)
                    {
                        this.IsProgressBarTooltipVisible = true;
                    }
                });
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

            this.IsProgressBarTooltipVisible = false;
        }

        private void ResetQueryHistoryIndex()
        {
            this.Results.RemoveResultsFor(QueryHistoryStorage.MetaData);
            QueryHistoryStorage.Instance.Reset();
        }

        private void UpdateResultViewInternal(List<Result> list, PluginMetadata metadata)
        {
            Infrastructure.Stopwatch.Normal($"UI update cost for {metadata.Name}",
                    () => { this.Results.AddResults(list, metadata.ID); });
        }

        private void BackToSearchMode()
        {
            this.QueryText = _textBeforeEnterContextMenuMode;
            this.IsContextMenuVisible = false;
            this.IsResultListBoxVisible = true;
            this.CaretIndex = this.QueryText.Length;
        }

        private void DisplayQueryHistory(HistoryItem history)
        {
            if (history != null)
            {
                var historyMetadata = QueryHistoryStorage.MetaData;

                this.QueryText = history.Query;
                this.SelectAllText = true;

                var executeQueryHistoryTitle = InternationalizationManager.Instance.GetTranslation("executeQuery");
                var lastExecuteTime = InternationalizationManager.Instance.GetTranslation("lastExecuteTime");
                this.Results.RemoveResultsExcept(historyMetadata);
                UpdateResultViewInternal(new List<Result>
                {
                    new Result
                    {
                        Title = string.Format(executeQueryHistoryTitle,history.Query),
                        SubTitle = string.Format(lastExecuteTime,history.ExecutedDateTime),
                        IcoPath = "Images\\history.png",
                        PluginDirectory = WoxDirectroy.Executable,
                        Action = _ =>{

                            this.QueryText = history.Query;
                            this.SelectAllText = true;

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
            this.IsProgressBarTooltipVisible = false;

            list.ForEach(o =>
            {
                o.Score += UserSelectedRecordStorage.Instance.GetSelectedCount(o) * 5;
            });
            if (originQuery.RawQuery == _lastQuery.RawQuery)
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    UpdateResultViewInternal(list, metadata);
                });
            }

            if (list.Count > 0)
            {
                this.IsResultListBoxVisible = true;
            }
        }

        public void ShowContextMenu(List<Result> actions, string pluginID)
        {
            this.DisplayContextMenu(actions, pluginID);
        }

        #endregion

        public event EventHandler<ListeningKeyPressedEventArgs> ListeningKeyPressed;

    }

    public class ListeningKeyPressedEventArgs : EventArgs
    {

        public System.Windows.Input.KeyEventArgs KeyEventArgs
        {
            get;
            private set;
        }

        public ListeningKeyPressedEventArgs(System.Windows.Input.KeyEventArgs keyEventArgs)
        {
            this.KeyEventArgs = keyEventArgs;
        }

    }
}
