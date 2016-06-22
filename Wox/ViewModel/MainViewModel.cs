using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using NHotkey;
using NHotkey.Wpf;
using Wox.Core.Plugin;
using Wox.Core.Resource;
using Wox.Helper;
using Wox.Infrastructure;
using Wox.Infrastructure.Hotkey;
using Wox.Infrastructure.Image;
using Wox.Infrastructure.Storage;
using Wox.Infrastructure.UserSettings;
using Wox.Plugin;
using Wox.Storage;

namespace Wox.ViewModel
{
    public class MainViewModel : BaseModel, ISavable
    {
        #region Private Fields

        private bool _queryHasReturn;
        private Query _lastQuery;
        private string _queryTextBeforeLoadContextMenu;
        private string _queryText;

        private readonly JsonStrorage<QueryHistory> _queryHistoryStorage;
        private readonly JsonStrorage<UserSelectedRecord> _userSelectedRecordStorage;
        private readonly JsonStrorage<TopMostRecord> _topMostRecordStorage;
        private readonly Settings _settings;
        private readonly QueryHistory _queryHistory;
        private readonly UserSelectedRecord _userSelectedRecord;
        private readonly TopMostRecord _topMostRecord;

        private CancellationTokenSource _updateSource;
        private CancellationToken _updateToken;
        private bool _saved;

        #endregion

        #region Constructor

        public MainViewModel(Settings settings)
        {
            _saved = false;
            _queryTextBeforeLoadContextMenu = "";
            _queryText = "";
            _lastQuery = new Query();

            _settings = settings;

            _queryHistoryStorage = new JsonStrorage<QueryHistory>();
            _userSelectedRecordStorage = new JsonStrorage<UserSelectedRecord>();
            _topMostRecordStorage = new JsonStrorage<TopMostRecord>();
            _queryHistory = _queryHistoryStorage.Load();
            _userSelectedRecord = _userSelectedRecordStorage.Load();
            _topMostRecord = _topMostRecordStorage.Load();

            ContextMenu = new ResultsViewModel(_settings);
            Results = new ResultsViewModel(_settings);
            _selectedResults = Results;

            InitializeKeyCommands();
            RegisterResultsUpdatedEvent();

            SetHotkey(_settings.Hotkey, OnHotkey);
            SetCustomPluginHotkey();
        }

        private void RegisterResultsUpdatedEvent()
        {
            foreach (var pair in PluginManager.GetPluginsForInterface<IResultUpdated>())
            {
                var plugin = (IResultUpdated)pair.Plugin;
                plugin.ResultsUpdated += (s, e) =>
                {
                    Task.Run(() =>
                    {

                        PluginManager.UpdatePluginMetadata(e.Results, pair.Metadata, e.Query);
                        UpdateResultView(e.Results, pair.Metadata, e.Query);
                    }, _updateToken);
                };
            }
        }


        private void InitializeKeyCommands()
        {
            EscCommand = new RelayCommand(_ =>
            {
                if (!ResultsSelected())
                {
                    SelectedResults = Results;
                }
                else
                {
                    MainWindowVisibility = Visibility.Collapsed;
                }
            });

            SelectNextItemCommand = new RelayCommand(_ =>
            {
                SelectedResults.SelectNextResult();
            });

            SelectPrevItemCommand = new RelayCommand(_ =>
            {
                SelectedResults.SelectPrevResult();
            });


            /**
            DisplayNextQueryCommand = new RelayCommand(_ =>
            {
                var nextQuery = _queryHistory.Next();
                DisplayQueryHistory(nextQuery);
            });

            DisplayPrevQueryCommand = new RelayCommand(_ =>
            {
                var prev = _queryHistory.Previous();
                DisplayQueryHistory(prev);
            });
            **/

            SelectNextPageCommand = new RelayCommand(_ =>
            {
                SelectedResults.SelectNextPage();
            });

            SelectPrevPageCommand = new RelayCommand(_ =>
            {
                SelectedResults.SelectPrevPage();
            });

            StartHelpCommand = new RelayCommand(_ =>
            {
                Process.Start("http://doc.getwox.com");
            });

            OpenResultCommand = new RelayCommand(index =>
            {
                var results = SelectedResults;

                if (index != null)
                {
                    results.SelectedIndex = int.Parse(index.ToString());
                }

                var result = results.SelectedItem?.Result;
                if (result != null) // SelectedItem returns null if selection is empty.
                {
                    bool hideWindow = result.Action != null && result.Action(new ActionContext
                    {
                        SpecialKeyState = GlobalHotkey.Instance.CheckModifiers()
                    });

                    if (hideWindow)
                    {
                        MainWindowVisibility = Visibility.Collapsed;
                    }

                    if (ResultsSelected())
                    {
                        _userSelectedRecord.Add(result);
                        _queryHistory.Add(result.OriginQuery.RawQuery);
                    }
                }
            });

            LoadContextMenuCommand = new RelayCommand(_ =>
            {
                if (ResultsSelected())
                {
                    SelectedResults = ContextMenu;
                }
                else
                {
                    SelectedResults = Results;
                }
            });

        }

        #endregion

        #region ViewModel Properties

        public ResultsViewModel Results { get; private set; }

        public ResultsViewModel ContextMenu { get; private set; }

        public string QueryText
        {
            get { return _queryText; }
            set
            {
                _queryText = value;
                ProgressBarVisibility = Visibility.Hidden;

                _updateSource?.Cancel();
                _updateSource = new CancellationTokenSource();
                _updateToken = _updateSource.Token;

                if (ResultsSelected())
                {
                    QueryResults();
                }
                else
                {
                    QueryContextMenu();
                }
            }
        }



        public bool QueryTextSelected { get; set; }

        private ResultsViewModel _selectedResults;

        private ResultsViewModel SelectedResults
        {
            get { return _selectedResults; }
            set
            {
                _selectedResults = value;
                if (ResultsSelected())
                {
                    QueryText = _queryTextBeforeLoadContextMenu;
                    ContextMenu.Visbility = Visibility.Collapsed;
                }
                else
                {
                    _queryTextBeforeLoadContextMenu = QueryText;
                    QueryText = "";
                    Results.Visbility = Visibility.Collapsed;
                }
                _selectedResults.Visbility = Visibility.Visible;
            }
        }

        public Visibility ProgressBarVisibility { get; set; }

        public Visibility MainWindowVisibility { get; set; }

        public ICommand EscCommand { get; set; }
        public ICommand SelectNextItemCommand { get; set; }
        public ICommand SelectPrevItemCommand { get; set; }
        //todo happlebao restore history command
        public ICommand DisplayNextQueryCommand { get; set; }
        public ICommand DisplayPrevQueryCommand { get; set; }
        public ICommand SelectNextPageCommand { get; set; }
        public ICommand SelectPrevPageCommand { get; set; }
        public ICommand StartHelpCommand { get; set; }
        public ICommand LoadContextMenuCommand { get; set; }
        public ICommand OpenResultCommand { get; set; }

        #endregion

        #region Private Methods

        private void QueryContextMenu()
        {
            const string contextMenuId = "Context Menu Id";
            var query = QueryText.ToLower().Trim();
            ContextMenu.Clear();

            var selected = Results.SelectedItem?.Result;

            if (selected != null) // SelectedItem returns null if selection is empty.
            {
                var id = selected.PluginID;

                var results = PluginManager.GetContextMenusForPlugin(selected);
                results.Add(ContextMenuTopMost(selected));
                results.Add(ContextMenuPluginInfo(id));

                if (!string.IsNullOrEmpty(query))
                {
                    var filtered = results.Where
                    (
                        r => StringMatcher.IsMatch(r.Title, query) ||
                             StringMatcher.IsMatch(r.SubTitle, query)
                    ).ToList();
                    ContextMenu.AddResults(filtered, contextMenuId);
                }
                else
                {
                    ContextMenu.AddResults(results, contextMenuId);
                }
            }
        }

        private void QueryResults()
        {
            if (!string.IsNullOrEmpty(QueryText))
            {
                Query(QueryText.Trim());
                //reset query history index after user start new query
                ResetQueryHistoryIndex();
            }
            else
            {
                Results.Clear();
                Results.Visbility = Visibility.Collapsed;
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
                Task.Delay(200, _updateToken).ContinueWith(_ =>
                {
                    if (query.RawQuery == _lastQuery.RawQuery && !_queryHasReturn)
                    {
                        ProgressBarVisibility = Visibility.Visible;
                    }
                }, _updateToken);

                var plugins = PluginManager.ValidPluginsForQuery(query);
                Task.Run(() =>
                {
                    Parallel.ForEach(plugins, plugin =>
                    {
                        var config = _settings.PluginSettings.Plugins[plugin.Metadata.ID];
                        if (!config.Disabled)
                        {

                            var results = PluginManager.QueryForPlugin(plugin, query);
                            UpdateResultView(results, plugin.Metadata, query);
                        }
                    });
                }, _updateToken);
            }
        }

        private void ResetQueryHistoryIndex()
        {
            Results.RemoveResultsFor(QueryHistory.MetaData);
            _queryHistory.Reset();
        }
        /**
        private void DisplayQueryHistory(HistoryItem history)
        {
            if (history != null)
            {
                var historyMetadata = QueryHistory.MetaData;

                QueryText = history.Query;
                OnTextBoxSelected();

                var executeQueryHistoryTitle = InternationalizationManager.Instance.GetTranslation("executeQuery");
                var lastExecuteTime = InternationalizationManager.Instance.GetTranslation("lastExecuteTime");
                Results.RemoveResultsExcept(historyMetadata);
                var result = new Result
                {
                    Title = string.Format(executeQueryHistoryTitle, history.Query),
                    SubTitle = string.Format(lastExecuteTime, history.ExecutedDateTime),
                    IcoPath = "Images\\history.png",
                    PluginDirectory = Infrastructure.Constant.ProgramDirectory,
                    Action = _ =>
                    {
                        QueryText = history.Query;
                        OnTextBoxSelected();
                        return false;
                    }
                };
                Task.Run(() =>
                {
                    Results.AddResults(new List<Result> {result}, historyMetadata.ID);
                }, _updateToken);
            }
        }
        **/

        private Result ContextMenuTopMost(Result result)
        {
            Result menu;
            if (_topMostRecord.IsTopMost(result))
            {
                menu = new Result
                {
                    Title = InternationalizationManager.Instance.GetTranslation("cancelTopMostInThisQuery"),
                    IcoPath = "Images\\down.png",
                    PluginDirectory = Constant.ProgramDirectory,
                    Action = _ =>
                    {
                        _topMostRecord.Remove(result);
                        App.API.ShowMsg("Succeed");
                        return false;
                    }
                };
            }
            else
            {
                menu = new Result
                {
                    Title = InternationalizationManager.Instance.GetTranslation("setAsTopMostInThisQuery"),
                    IcoPath = "Images\\up.png",
                    PluginDirectory = Constant.ProgramDirectory,
                    Action = _ =>
                    {
                        _topMostRecord.AddOrUpdate(result);
                        App.API.ShowMsg("Succeed");
                        return false;
                    }
                };
            }
            return menu;
        }

        private Result ContextMenuPluginInfo(string id)
        {
            var metadata = PluginManager.GetPluginForId(id).Metadata;
            var translator = InternationalizationManager.Instance;

            var author = translator.GetTranslation("author");
            var website = translator.GetTranslation("website");
            var version = translator.GetTranslation("version");
            var plugin = translator.GetTranslation("plugin");
            var title = $"{plugin}: {metadata.Name}";
            var icon = metadata.IcoPath;
            var subtitle = $"{author}: {metadata.Author}, {website}: {metadata.Website} {version}: {metadata.Version}";

            var menu = new Result
            {
                Title = title,
                IcoPath = icon,
                SubTitle = subtitle,
                PluginDirectory = metadata.PluginDirectory,
                Action = _ => false
            };
            return menu;
        }

        private bool ResultsSelected()
        {
            var selected = SelectedResults == Results;
            return selected;
        }

        #endregion
        #region Hotkey

        private void SetHotkey(string hotkeyStr, EventHandler<HotkeyEventArgs> action)
        {
            var hotkey = new HotkeyModel(hotkeyStr);
            SetHotkey(hotkey, action);
        }

        private void SetHotkey(HotkeyModel hotkey, EventHandler<HotkeyEventArgs> action)
        {
            string hotkeyStr = hotkey.ToString();
            try
            {
                HotkeyManager.Current.AddOrReplace(hotkeyStr, hotkey.CharKey, hotkey.ModifierKeys, action);
            }
            catch (Exception)
            {
                string errorMsg =
                    string.Format(InternationalizationManager.Instance.GetTranslation("registerHotkeyFailed"), hotkeyStr);
                MessageBox.Show(errorMsg);
            }
        }

        public void RemoveHotkey(string hotkeyStr)
        {
            if (!string.IsNullOrEmpty(hotkeyStr))
            {
                HotkeyManager.Current.Remove(hotkeyStr);
            }
        }

        /// <summary>
        /// Checks if Wox should ignore any hotkeys
        /// </summary>
        /// <returns></returns>
        private bool ShouldIgnoreHotkeys()
        {
            //double if to omit calling win32 function
            if (_settings.IgnoreHotkeysOnFullscreen)
                if (WindowIntelopHelper.IsWindowFullscreen())
                    return true;

            return false;
        }

        private void SetCustomPluginHotkey()
        {
            if (_settings.CustomPluginHotkeys == null) return;
            foreach (CustomPluginHotkey hotkey in _settings.CustomPluginHotkeys)
            {
                SetHotkey(hotkey.Hotkey, (s, e) =>
                {
                    if (ShouldIgnoreHotkeys()) return;
                    QueryText = hotkey.ActionKeyword;
                    MainWindowVisibility = Visibility.Visible;
                });
            }
        }

        private void OnHotkey(object sender, HotkeyEventArgs e)
        {
            if (ShouldIgnoreHotkeys()) return;
            QueryTextSelected = true;
            ToggleWox();
            e.Handled = true;
        }

        private void ToggleWox()
        {
            if (MainWindowVisibility != Visibility.Visible)
            {
                MainWindowVisibility = Visibility.Visible;
            }
            else
            {
                MainWindowVisibility = Visibility.Collapsed;
            }
        }

        #endregion

        #region Public Methods

        public void Save()
        {
            if (!_saved)
            {
                _queryHistoryStorage.Save();
                _userSelectedRecordStorage.Save();
                _topMostRecordStorage.Save();

                PluginManager.Save();
                ImageLoader.Save();

                _saved = true;
            }
        }

        /// <summary>
        /// To avoid deadlock, this method should not called from main thread
        /// </summary>
        public void UpdateResultView(List<Result> list, PluginMetadata metadata, Query originQuery)
        {
            _queryHasReturn = true;
            ProgressBarVisibility = Visibility.Hidden;

            foreach (var result in list)
            {
                if (_topMostRecord.IsTopMost(result))
                {
                    result.Score = int.MaxValue;
                }
                else
                {
                    result.Score += _userSelectedRecord.GetSelectedCount(result) * 5;
                }
            }

            if (originQuery.RawQuery == _lastQuery.RawQuery)
            {
                Results.AddResults(list, metadata.ID);
            }

            if (Results.Visbility != Visibility.Visible && list.Count > 0)
            {
                Results.Visbility = Visibility.Visible;
            }
        }

        #endregion
    }
}