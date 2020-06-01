using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using NHotkey;
using NHotkey.Wpf;
using Wox.Core.Plugin;
using Wox.Core.Resource;
using Wox.Helper;
using Wox.Infrastructure;
using Wox.Infrastructure.Hotkey;
using Wox.Infrastructure.Storage;
using Wox.Infrastructure.UserSettings;
using Wox.Plugin;
using Microsoft.PowerLauncher.Telemetry;
using Wox.Storage;
using Microsoft.PowerToys.Telemetry;

namespace Wox.ViewModel
{
    public class MainViewModel : BaseModel, ISavable
    {
        #region Private Fields

        private bool _isQueryRunning;
        private Query _lastQuery;
        private string _queryTextBeforeLeaveResults;

        private readonly WoxJsonStorage<History> _historyItemsStorage;
        private readonly WoxJsonStorage<UserSelectedRecord> _userSelectedRecordStorage;
        private readonly WoxJsonStorage<TopMostRecord> _topMostRecordStorage;
        private readonly Settings _settings;
        private readonly History _history;
        private readonly UserSelectedRecord _userSelectedRecord;
        private readonly TopMostRecord _topMostRecord;

        private CancellationTokenSource _updateSource;
        private CancellationToken _updateToken;
        private bool _saved;

        private readonly Internationalization _translator = InternationalizationManager.Instance;

        #endregion

        #region Constructor

        public MainViewModel(Settings settings)
        {
            _saved = false;
            _queryTextBeforeLeaveResults = "";
            _lastQuery = new Query();

            _settings = settings;

            _historyItemsStorage = new WoxJsonStorage<History>();
            _userSelectedRecordStorage = new WoxJsonStorage<UserSelectedRecord>();
            _topMostRecordStorage = new WoxJsonStorage<TopMostRecord>();
            _history = _historyItemsStorage.Load();
            _userSelectedRecord = _userSelectedRecordStorage.Load();
            _topMostRecord = _topMostRecordStorage.Load();

            ContextMenu = new ResultsViewModel(_settings);
            Results = new ResultsViewModel(_settings);
            History = new ResultsViewModel(_settings);
            _selectedResults = Results;

            InitializeKeyCommands();
            RegisterResultsUpdatedEvent();

            _settings.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(Settings.Hotkey))
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        if (_settings.PreviousHotkey != "")
                        {
                            RemoveHotkey(_settings.PreviousHotkey);
                        }

                        if (_settings.Hotkey != "")
                        {
                            SetHotkey(_settings.Hotkey, OnHotkey);
                        }
                    });
                }
            };

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
                if (!SelectedIsFromQueryResults())
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

            SelectNextTabItemCommand = new RelayCommand(_ =>
            {
                SelectedResults.SelectNextTabItem();
            });

            SelectPrevTabItemCommand = new RelayCommand(_ =>
            {
                SelectedResults.SelectPrevTabItem();
            });

            SelectNextPageCommand = new RelayCommand(_ =>
            {
                SelectedResults.SelectNextPage();
            });

            SelectPrevPageCommand = new RelayCommand(_ =>
            {
                SelectedResults.SelectPrevPage();
            });

            SelectFirstResultCommand = new RelayCommand(_ => SelectedResults.SelectFirstResult());

            StartHelpCommand = new RelayCommand(_ =>
            {
                Process.Start("http://doc.wox.one/");
            });

            OpenResultCommand = new RelayCommand(index =>
            {
                var results = SelectedResults;

                if (index != null)
                {
                    results.SelectedIndex = int.Parse(index.ToString());
                }

                if(results.SelectedItem != null)
                {
                    //If there is a context button selected fire the action for that button before the main command. 
                    bool didExecuteContextButton = results.SelectedItem.ExecuteSelectedContextButton();

                    if (!didExecuteContextButton)
                    {
                        var result = results.SelectedItem.Result;
                        if (result != null && result.Action != null) // SelectedItem returns null if selection is empty.
                        {
                            MainWindowVisibility = Visibility.Collapsed;

                            Task.Run(() =>
                            {
                                result.Action(new ActionContext
                                {
                                    SpecialKeyState = GlobalHotkey.Instance.CheckModifiers()
                                });
                            });

                            if (SelectedIsFromQueryResults())
                            {
                                _userSelectedRecord.Add(result);
                                _history.Add(result.OriginQuery.RawQuery);
                            }
                            else
                            {
                                SelectedResults = Results;
                            }
                        }
                    }
                }              
            });

            LoadContextMenuCommand = new RelayCommand(_ =>
            {
                if (SelectedIsFromQueryResults())
                {
                    SelectedResults = ContextMenu;
                }
                else
                {
                    SelectedResults = Results;
                }
            });

            LoadHistoryCommand = new RelayCommand(_ =>
            {
                if (SelectedIsFromQueryResults())
                {
                    SelectedResults = History;
                    History.SelectedIndex = _history.Items.Count - 1;
                }
                else
                {
                    SelectedResults = Results;
                }
            });
        }

        #endregion

        #region ViewModel Properties

        public Brush MainWindowBackground { get; set; }
        public Brush MainWindowBorderBrush { get; set; }

        public ResultsViewModel Results { get; private set; }
        public ResultsViewModel ContextMenu { get; private set; }
        public ResultsViewModel History { get; private set; }

        public string SystemQueryText { get; set; } = String.Empty;

        public string QueryText { get; set; } = String.Empty;
      

        /// <summary>
        /// we need move cursor to end when we manually changed query
        /// but we don't want to move cursor to end when query is updated from TextBox. 
        /// Also we don't want to force the results to change unless explicitly told to.
        /// </summary>
        /// <param name="queryText"></param>
        /// <param name="requery">Optional Parameter that if true, will automatically execute a query against the updated text</param>
        public void ChangeQueryText(string queryText, bool requery=false)
        {
            SystemQueryText = queryText;
            
            if(requery)
            {
                QueryText = queryText;
                Query();
            }
        }
        public bool LastQuerySelected { get; set; }

        private ResultsViewModel _selectedResults;
        private ResultsViewModel SelectedResults
        {
            get { return _selectedResults; }
            set
            {
                _selectedResults = value;
                if (SelectedIsFromQueryResults())
                {
                    ContextMenu.Visibility = Visibility.Collapsed;
                    History.Visibility = Visibility.Collapsed;
                    ChangeQueryText(_queryTextBeforeLeaveResults);
                }
                else
                {
                    Results.Visibility = Visibility.Collapsed;
                    _queryTextBeforeLeaveResults = QueryText;


                    // Because of Fody's optimization
                    // setter won't be called when property value is not changed.
                    // so we need manually call Query()
                    // http://stackoverflow.com/posts/25895769/revisions
                    if (string.IsNullOrEmpty(QueryText))
                    {
                        Query();
                    }
                    else
                    {
                        QueryText = string.Empty;
                    }
                }
                _selectedResults.Visibility = Visibility.Visible;
            }
        }

        public Visibility ProgressBarVisibility { get; set; }

        private Visibility _visibility;

        public Visibility MainWindowVisibility {
            get { return _visibility; }
            set {
                _visibility = value;
                if(value == Visibility.Visible)
                {
                    PowerToysTelemetry.Log.WriteEvent(new LauncherShowEvent());
                }
                else
                {
                    PowerToysTelemetry.Log.WriteEvent(new LauncherHideEvent());
                }
            
            }
        }

        public ICommand EscCommand { get; set; }
        public ICommand SelectNextItemCommand { get; set; }
        public ICommand SelectPrevItemCommand { get; set; }

        public ICommand SelectNextTabItemCommand { get; set; }
        public ICommand SelectPrevTabItemCommand { get; set; }

        public ICommand SelectNextPageCommand { get; set; }
        public ICommand SelectPrevPageCommand { get; set; }
        public ICommand SelectFirstResultCommand { get; set; }
        public ICommand StartHelpCommand { get; set; }
        public ICommand LoadContextMenuCommand { get; set; }
        public ICommand LoadHistoryCommand { get; set; }
        public ICommand OpenResultCommand { get; set; }

        #endregion

        public void Query()
        {
            if (SelectedIsFromQueryResults())
            {
                QueryResults();
            }
            else if (HistorySelected())
            {
                QueryHistory();
            }
        }

        private void QueryHistory()
        {
            const string id = "Query History ID";
            var query = QueryText.ToLower().Trim();
            History.Clear();

            var results = new List<Result>();
            foreach (var h in _history.Items)
            {
                var title = _translator.GetTranslation("executeQuery");
                var time = _translator.GetTranslation("lastExecuteTime");
                var result = new Result
                {
                    Title = string.Format(title, h.Query),
                    SubTitle = string.Format(time, h.ExecutedDateTime),
                    IcoPath = "Images\\history.png",
                    OriginQuery = new Query { RawQuery = h.Query },
                    Action = _ =>
                    {
                        SelectedResults = Results;
                        ChangeQueryText(h.Query);
                        return false;
                    }
                };
                results.Add(result);
            }

            if (!string.IsNullOrEmpty(query))
            {
                var filtered = results.Where
                (
                    r => StringMatcher.FuzzySearch(query, r.Title).IsSearchPrecisionScoreMet() ||
                         StringMatcher.FuzzySearch(query, r.SubTitle).IsSearchPrecisionScoreMet()
                ).ToList();
                History.AddResults(filtered, id);
            }
            else
            {
                History.AddResults(results, id);
            }
        }

        private void QueryResults()
        {
            if (!string.IsNullOrEmpty(QueryText))
            {
                var queryTimer = new System.Diagnostics.Stopwatch();
                queryTimer.Start();
                _updateSource?.Cancel();
                var currentUpdateSource = new CancellationTokenSource();
                _updateSource = currentUpdateSource;
                var currentCancellationToken = _updateSource.Token;
                _updateToken = currentCancellationToken;

                ProgressBarVisibility = Visibility.Hidden;
                _isQueryRunning = true;
                var query = QueryBuilder.Build(QueryText.Trim(), PluginManager.NonGlobalPlugins);
                if (query != null)
                {
                    // handle the exclusiveness of plugin using action keyword
                    RemoveOldQueryResults(query);

                    _lastQuery = query;
                    var plugins = PluginManager.ValidPluginsForQuery(query);

                    Task.Run(() =>
                    {
                        // so looping will stop once it was cancelled
                        var parallelOptions = new ParallelOptions { CancellationToken = currentCancellationToken };
                        try
                        {
                            Parallel.ForEach(plugins, parallelOptions, plugin =>
                            {
                                if (!plugin.Metadata.Disabled)
                                {
                                    var results = PluginManager.QueryForPlugin(plugin, query);
                                    if (Application.Current.Dispatcher.CheckAccess())
                                    {
                                        UpdateResultView(results, plugin.Metadata, query);
                                    }
                                    else
                                    {
                                        Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                                        {
                                            UpdateResultView(results, plugin.Metadata, query);
                                        }));
                                    }
                                }
                            });
                        }
                        catch (OperationCanceledException)
                        {
                            // nothing to do here
                        }


                        // this should happen once after all queries are done so progress bar should continue
                        // until the end of all querying
                        _isQueryRunning = false;
                        if (currentUpdateSource == _updateSource)
                        { // update to hidden if this is still the current query
                            ProgressBarVisibility = Visibility.Hidden;
                        }

                        queryTimer.Stop();
                        var queryEvent = new LauncherQueryEvent()
                        {
                            QueryTimeMs = queryTimer.ElapsedMilliseconds,
                            NumResults = Results.Results.Count,
                            QueryLength = query.RawQuery.Length
                        };
                        PowerToysTelemetry.Log.WriteEvent(queryEvent);

                    }, currentCancellationToken);
                }
            }
            else
            {
                Results.SelectedItem = null;
                Results.Clear();                
                Results.Visibility = Visibility.Collapsed;
            }
        }

        private void RemoveOldQueryResults(Query query)
        {
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
        }


        private bool SelectedIsFromQueryResults()
        {
            var selected = SelectedResults == Results;
            return selected;
        }

        private bool ContextMenuSelected()
        {
            var selected = SelectedResults == ContextMenu;
            return selected;
        }


        private bool HistorySelected()
        {
            var selected = SelectedResults == History;
            return selected;
        }
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
                if (WindowsInteropHelper.IsWindowFullscreen())
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
                    MainWindowVisibility = Visibility.Visible;
                    ChangeQueryText(hotkey.ActionKeyword);
                });
            }
        }

        private void OnHotkey(object sender, HotkeyEventArgs e)
        {
            if (!ShouldIgnoreHotkeys())
            {

                if (_settings.LastQueryMode == LastQueryMode.Empty)
                {
                    ChangeQueryText(string.Empty);
                }
                else if (_settings.LastQueryMode == LastQueryMode.Preserved)
                {
                    LastQuerySelected = true;
                }
                else if (_settings.LastQueryMode == LastQueryMode.Selected)
                {
                    LastQuerySelected = false;
                }
                else
                {
                    throw new ArgumentException($"wrong LastQueryMode: <{_settings.LastQueryMode}>");
                }

                ToggleWox();
                e.Handled = true;
            }
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
                _historyItemsStorage.Save();
                _userSelectedRecordStorage.Save();
                _topMostRecordStorage.Save();

                _saved = true;
            }
        }

        /// <summary>
        /// To avoid deadlock, this method should not called from main thread
        /// </summary>
        public void UpdateResultView(List<Result> list, PluginMetadata metadata, Query originQuery)
        {
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

            if (Results.Visibility != Visibility.Visible && list.Count > 0)
            {
                Results.Visibility = Visibility.Visible;
            }
        }

        public void ColdStartFix()
        {
            // Fix Cold start for List view xaml island
            List<Result> list = new List<Result>();
            Result r = new Result
            {
                Title = "hello"
            };
            list.Add(r);
            Results.AddResults(list, "0");
            Results.Clear();
            MainWindowVisibility = System.Windows.Visibility.Collapsed;

            // Fix Cold start for plugins
            string s = "m";
            var query = QueryBuilder.Build(s.Trim(), PluginManager.NonGlobalPlugins);
            var plugins = PluginManager.ValidPluginsForQuery(query);
            foreach (PluginPair plugin in plugins)
            {
                if (!plugin.Metadata.Disabled && plugin.Metadata.Name != "Window Walker")
                {
                    var _ = PluginManager.QueryForPlugin(plugin, query);
                }
            };
        }

        public void HandleContextMenu(Key AcceleratorKey, ModifierKeys AcceleratorModifiers)
        {
            var results = SelectedResults;
            if (results.SelectedItem != null)
            {
                foreach (ContextMenuItemViewModel contextMenuItems in results.SelectedItem.ContextMenuItems)
                {
                    if (contextMenuItems.AcceleratorKey == AcceleratorKey && contextMenuItems.AcceleratorModifiers == AcceleratorModifiers)
                    {
                        MainWindowVisibility = Visibility.Collapsed;
                        contextMenuItems.Command.Execute(null);
                    }
                }
            }
        }

        #endregion
    }
}