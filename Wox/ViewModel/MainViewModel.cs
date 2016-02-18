using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Wox.Core.Plugin;
using Wox.Core.Resource;
using Wox.Infrastructure;
using Wox.Infrastructure.Hotkey;
using Wox.Plugin;
using Wox.Storage;

namespace Wox.ViewModel
{
    public class MainViewModel : BaseViewModel
    {
        #region Private Fields

        private ResultPanelViewModel _searchResultPanel;
        private ResultPanelViewModel _actionPanel;
        private string _queryText;
        private bool _isVisible;
        private bool _isSearchResultPanelVisible;
        private bool _isActionPanelVisible;
        private bool _isProgressBarVisible;
        private bool _isProgressBarTooltipVisible;
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
            this.InitializeResultPanel();
            this.InitializeActionPanel();
            this.InitializeKeyCommands();

            this._queryHasReturn = false;
        }

        #endregion

        #region ViewModel Properties

        public ResultPanelViewModel SearchResultPanel
        {
            get
            {
                return this._searchResultPanel;
            }
        }

        public ResultPanelViewModel ActionPanel
        {
            get
            {
                return this._actionPanel;
            }
        }

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

                if (!value && this.IsActionPanelVisible)
                {
                    this.BackToSearchMode();
                }
            }
        }

        public bool IsSearchResultPanelVisible
        {
            get
            {
                return this._isSearchResultPanelVisible;
            }
            set
            {
                this._isSearchResultPanelVisible = value;
                OnPropertyChanged("IsSearchResultPanelVisible");
            }
        }

        public bool IsActionPanelVisible
        {
            get
            {
                return this._isActionPanelVisible;
            }
            set
            {
                this._isActionPanelVisible = value;
                OnPropertyChanged("IsActionPanelVisible");
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

        #endregion

        #region Private Methods

        private void InitializeKeyCommands()
        {
            this.EscCommand = new RelayCommand(() => {

                if (this.IsActionPanelVisible)
                {
                    this.BackToSearchMode();
                }
                else
                {
                    this.IsVisible = false;
                }

            });
        }

        private void InitializeResultPanel()
        {
            this._searchResultPanel = new ResultPanelViewModel();
            this.IsSearchResultPanelVisible = false;
            this._searchResultPanel.ResultOpenedInPanel += (o, e) =>
            {
                if (e.HideWindow)
                {
                    this.IsVisible = false;
                }
                UserSelectedRecordStorage.Instance.Add(e.Result.RawResult);
                QueryHistoryStorage.Instance.Add(this.QueryText);
            };

            this._searchResultPanel.ResultActionPanelOpenedInPanel += (o, e) =>
            {
                this.ShowActionPanel(e.Result.RawResult);
            };
        }

        private void ShowActionPanel(Result result)
        {
            if (result == null) return;
            List<Result> results = PluginManager.GetContextMenusForPlugin(result);
            results.ForEach(o =>
            {
                o.PluginDirectory = PluginManager.GetPluginForId(result.PluginID).Metadata.PluginDirectory;
                o.PluginID = result.PluginID;
                o.OriginQuery = result.OriginQuery;
            });

            results.Add(GetTopMostContextMenu(result));

            _textBeforeEnterContextMenuMode = this.QueryText;

            this._actionPanel.Clear();
            this._actionPanel.AddResults(results, result.PluginID);
            CurrentContextMenus = results;

            this.IsActionPanelVisible = true;
            this.IsSearchResultPanelVisible = false;

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
                        //TODO:Modify the way showing this message
                        //ShowMsg("Succeed", "", "");
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
                        //TODO:Modify the way showing this message
                        //ShowMsg("Succeed", "", "");
                        return false;
                    }
                };
            }
        }

        private void InitializeActionPanel()
        {
            this._actionPanel = new ResultPanelViewModel();
            this.IsActionPanelVisible = false;
            this._actionPanel.ResultOpenedInPanel += (o, e) =>
            {
                if (e.HideWindow)
                {
                    this.IsVisible = false;
                }
            };
        }

        private void HandleQueryTextUpdated()
        {
            if (_ignoreTextChange) { _ignoreTextChange = false; return; }

            this.IsProgressBarTooltipVisible = false;
            if (this.IsActionPanelVisible)
            {
                QueryActionPanel();
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
                    this._searchResultPanel.Clear();
                }
            }
        }

        private void QueryActionPanel()
        {
            var contextMenuId = "Context Menu Id";
            this._actionPanel.Clear();
            var query = this.QueryText.ToLower();
            if (string.IsNullOrEmpty(query))
            {
                this._actionPanel.AddResults(CurrentContextMenus, contextMenuId);
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
                this._actionPanel.AddResults(filterResults, contextMenuId);
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
                        this._searchResultPanel.RemoveResultsExcept(PluginManager.NonGlobalPlugins[keyword].Metadata);
                    }
                }
                else
                {
                    if (string.IsNullOrEmpty(keyword))
                    {
                        this._searchResultPanel.RemoveResultsFor(PluginManager.NonGlobalPlugins[lastKeyword].Metadata);
                    }
                    else if (lastKeyword != keyword)
                    {
                        this._searchResultPanel.RemoveResultsExcept(PluginManager.NonGlobalPlugins[keyword].Metadata);
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
            this._searchResultPanel.RemoveResultsFor(QueryHistoryStorage.MetaData);
            QueryHistoryStorage.Instance.Reset();
        }

        private void UpdateResultViewInternal(List<Result> list, PluginMetadata metadata)
        {
            Stopwatch.Normal($"UI update cost for {metadata.Name}",
                    () => { this._searchResultPanel.AddResults(list, metadata.ID); });
        }

        private void BackToSearchMode()
        {
            this.QueryText = _textBeforeEnterContextMenuMode;
            this.IsActionPanelVisible = false;
            this.IsSearchResultPanelVisible = true;
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
                Application.Current.Dispatcher.Invoke(() => {
                    UpdateResultViewInternal(list, metadata);
                });
            }

            if(list.Count > 0)
            {
                this.IsSearchResultPanelVisible = true;
            }
        }

        #endregion

    }
}
