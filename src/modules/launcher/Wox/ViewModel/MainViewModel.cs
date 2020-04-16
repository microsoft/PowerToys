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
using Wox.Infrastructure.Hotkey;
using Wox.Infrastructure.Storage;
using Wox.Infrastructure.UserSettings;
using Wox.Plugin;
using Wox.Storage;

namespace Wox.ViewModel
{
    public class MainViewModel : BaseModel, ISavable
    {
        #region Private Fields

        private bool _isQueryRunning;
        private Query _lastQuery;

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
            _queryText = "";
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
                MainWindowVisibility = Visibility.Collapsed;
            });

            SelectNextItemCommand = new RelayCommand(_ =>
            {
                Results.SelectNextResult();
            });

            SelectPrevItemCommand = new RelayCommand(_ =>
            {
                Results.SelectPrevResult();
            });

            SelectNextPageCommand = new RelayCommand(_ =>
            {
                Results.SelectNextPage();
            });

            SelectPrevPageCommand = new RelayCommand(_ =>
            {
                Results.SelectPrevPage();
            });

            SelectFirstResultCommand = new RelayCommand(_ => Results.SelectFirstResult());

            StartHelpCommand = new RelayCommand(_ =>
            {
                Process.Start("http://doc.wox.one/");
            });

            OpenResultCommand = new RelayCommand(index =>
            {
                var results = Results;

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

                    _userSelectedRecord.Add(result);
                    _history.Add(result.OriginQuery.RawQuery);
                }
            });         
        }

        #endregion

        #region ViewModel Properties

        public ResultsViewModel Results { get; private set; }
        public ResultsViewModel ContextMenu { get; private set; }
        public ResultsViewModel History { get; private set; }

        private string _queryText;
        public string QueryText
        {
            get { return _queryText; }
            set
            {
                _queryText = value;
                Query();
            }
        }

        /// <summary>
        /// we need move cursor to end when we manually changed query
        /// but we don't want to move cursor to end when query is updated from TextBox
        /// </summary>
        /// <param name="queryText"></param>
        public void ChangeQueryText(string queryText)
        {
            QueryTextCursorMovedToEnd = true;
            QueryText = queryText;
        }
        public bool LastQuerySelected { get; set; }
        public bool QueryTextCursorMovedToEnd { get; set; }

        public Visibility ProgressBarVisibility { get; set; }

        public Visibility MainWindowVisibility { get; set; }

        public ICommand EscCommand { get; set; }
        public ICommand SelectNextItemCommand { get; set; }
        public ICommand SelectPrevItemCommand { get; set; }
        public ICommand SelectNextPageCommand { get; set; }
        public ICommand SelectPrevPageCommand { get; set; }
        public ICommand SelectFirstResultCommand { get; set; }
        public ICommand StartHelpCommand { get; set; }
        public ICommand OpenResultCommand { get; set; }

        #endregion

        public void Query()
        {

            QueryResults();
            
        }

        private void QueryResults()
        {
            if (!string.IsNullOrEmpty(QueryText))
            {
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
                    }, currentCancellationToken);
                }
            }
            else
            {
                Results.Clear();
                Results.Visbility = Windows.UI.Xaml.Visibility.Collapsed;
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

            if (Results.Visbility != Windows.UI.Xaml.Visibility.Visible && list.Count > 0)
            {
                Results.Visbility = Windows.UI.Xaml.Visibility.Visible;
            }
        }

        #endregion
    }
}