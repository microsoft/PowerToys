// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Common.UI;
using interop;
using Microsoft.PowerLauncher.Telemetry;
using Microsoft.PowerToys.Telemetry;
using PowerLauncher.Helper;
using PowerLauncher.Plugin;
using PowerLauncher.Storage;
using Wox.Infrastructure;
using Wox.Infrastructure.Hotkey;
using Wox.Infrastructure.Storage;
using Wox.Infrastructure.UserSettings;
using Wox.Plugin;
using Wox.Plugin.Logger;

namespace PowerLauncher.ViewModel
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Globalization", "CA1309:Use ordinal string comparison", Justification = "Using CurrentCultureIgnoreCase for user facing strings. Each usage is attributed with a comment.")]
    public class MainViewModel : BaseModel, IMainViewModel, ISavable, IDisposable
    {
        private string _currentQuery;
        private static string _emptyQuery = string.Empty;

        private static bool _disposed;

        private readonly WoxJsonStorage<QueryHistory> _historyItemsStorage;
        private readonly WoxJsonStorage<UserSelectedRecord> _userSelectedRecordStorage;
        private readonly PowerToysRunSettings _settings;
        private readonly QueryHistory _history;
        private readonly UserSelectedRecord _userSelectedRecord;
        private readonly object _addResultsLock = new object();
        private readonly System.Diagnostics.Stopwatch _hotkeyTimer = new System.Diagnostics.Stopwatch();

        private string _queryTextBeforeLeaveResults;

        private CancellationTokenSource _updateSource;

        private CancellationToken _updateToken;
        private CancellationToken _nativeWaiterCancelToken;
        private bool _saved;
        private ushort _hotkeyHandle;

        private const int _globalHotKeyId = 0x0001;
        private IntPtr _globalHotKeyHwnd;
        private uint _globalHotKeyVK;
        private uint _globalHotKeyFSModifiers;
        private bool _usingGlobalHotKey;

        internal HotkeyManager HotkeyManager { get; private set; }

        public MainViewModel(PowerToysRunSettings settings, CancellationToken nativeThreadCancelToken)
        {
            _saved = false;
            _queryTextBeforeLeaveResults = string.Empty;
            _currentQuery = _emptyQuery;
            _disposed = false;

            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _nativeWaiterCancelToken = nativeThreadCancelToken;
            _historyItemsStorage = new WoxJsonStorage<QueryHistory>();
            _userSelectedRecordStorage = new WoxJsonStorage<UserSelectedRecord>();
            _history = _historyItemsStorage.Load();
            _userSelectedRecord = _userSelectedRecordStorage.Load();

            ContextMenu = new ResultsViewModel(_settings, this);
            Results = new ResultsViewModel(_settings, this);
            History = new ResultsViewModel(_settings, this);
            _selectedResults = Results;

            InitializeKeyCommands();
            RegisterResultsUpdatedEvent();
        }

        public void RemoveUserSelectedRecord(Result result)
        {
            _userSelectedRecord.Remove(result);
        }

        public void RegisterHotkey(IntPtr hwnd)
        {
            Log.Info("RegisterHotkey()", GetType());

            // Allow OOBE to call PowerToys Run.
            NativeEventWaiter.WaitForEventLoop(Constants.PowerLauncherSharedEvent(), OnHotkey, Application.Current.Dispatcher, _nativeWaiterCancelToken);

            if (_settings.StartedFromPowerToysRunner)
            {
                // Allow runner to call PowerToys Run from the centralized keyboard hook.
                NativeEventWaiter.WaitForEventLoop(Constants.PowerLauncherCentralizedHookSharedEvent(), OnCentralizedKeyboardHookHotKey, Application.Current.Dispatcher, _nativeWaiterCancelToken);
            }

            _settings.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(PowerToysRunSettings.Hotkey) || e.PropertyName == nameof(PowerToysRunSettings.UseCentralizedKeyboardHook))
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        if (!string.IsNullOrEmpty(_settings.PreviousHotkey))
                        {
                            if (_usingGlobalHotKey)
                            {
                                NativeMethods.UnregisterHotKey(_globalHotKeyHwnd, _globalHotKeyId);
                                _usingGlobalHotKey = false;
                                Log.Info("Unregistering previous global hotkey", GetType());
                            }

                            if (_hotkeyHandle != 0)
                            {
                                HotkeyManager?.UnregisterHotkey(_hotkeyHandle);
                                _hotkeyHandle = 0;
                                Log.Info("Unregistering previous low level key handler", GetType());
                            }
                        }

                        if (!string.IsNullOrEmpty(_settings.Hotkey))
                        {
                            SetHotkey(hwnd, _settings.Hotkey, OnHotkey);
                        }
                    });
                }
            };

            SetHotkey(hwnd, _settings.Hotkey, OnHotkey);

            // TODO: Custom plugin hotkeys.
            // SetCustomPluginHotkey();
        }

        public void RegisterSettingsChangeListener(System.ComponentModel.PropertyChangedEventHandler handler)
        {
            _settings.PropertyChanged += handler;
        }

        private void RegisterResultsUpdatedEvent()
        {
            foreach (var pair in PluginManager.GetPluginsForInterface<IResultUpdated>())
            {
                var plugin = (IResultUpdated)pair.Plugin;
                plugin.ResultsUpdated += (s, e) =>
                {
                    Task.Run(
                        () =>
                        {
                            PluginManager.UpdatePluginMetadata(e.Results, pair.Metadata, e.Query);
                            UpdateResultView(e.Results, e.Query.RawQuery, _updateToken);
                        },
                        _updateToken);
                };
            }
        }

        private void OpenResultsEvent(object index, bool isMouseClick)
        {
            var results = SelectedResults;

            if (index != null)
            {
                // Using InvariantCulture since this is internal
                results.SelectedIndex = int.Parse(index.ToString(), CultureInfo.InvariantCulture);
            }

            if (results.SelectedItem != null)
            {
                bool executeResultRequired = false;

                if (isMouseClick)
                {
                    executeResultRequired = true;
                }
                else
                {
                    // If there is a context button selected fire the action for that button instead, and the main command will not be executed
                    executeResultRequired = !results.SelectedItem.ExecuteSelectedContextButton();
                }

                if (executeResultRequired)
                {
                    var result = results.SelectedItem.Result;

                    // SelectedItem returns null if selection is empty.
                    if (result != null && result.Action != null)
                    {
                        bool hideWindow = true;

                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            hideWindow = result.Action(new ActionContext
                            {
                                SpecialKeyState = KeyboardHelper.CheckModifiers(),
                            });
                        });

                        if (hideWindow)
                        {
                            Hide();
                        }

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
        }

        private void InitializeKeyCommands()
        {
            IgnoreCommand = new RelayCommand(_ => { });

            EscCommand = new RelayCommand(_ =>
            {
                if (!SelectedIsFromQueryResults())
                {
                    SelectedResults = Results;
                }
                else
                {
                    Hide();
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

            SelectNextContextMenuItemCommand = new RelayCommand(_ =>
            {
                SelectedResults.SelectNextContextMenuItem();
            });

            SelectPreviousContextMenuItemCommand = new RelayCommand(_ =>
            {
                SelectedResults.SelectPreviousContextMenuItem();
            });

            SelectNextPageCommand = new RelayCommand(_ =>
            {
                SelectedResults.SelectNextPage();
            });

            SelectPrevPageCommand = new RelayCommand(_ =>
            {
                SelectedResults.SelectPrevPage();
            });

            OpenResultWithKeyboardCommand = new RelayCommand(index =>
            {
                OpenResultsEvent(index, false);
            });

            OpenResultWithMouseCommand = new RelayCommand(index =>
            {
                OpenResultsEvent(index, true);
            });

            ClearQueryCommand = new RelayCommand(_ =>
            {
                if (!string.IsNullOrEmpty(QueryText))
                {
                    ChangeQueryText(string.Empty, true);

                    // Push Event to UI SystemQuery has changed
                    OnPropertyChanged(nameof(SystemQueryText));
                }
            });
        }

        private ResultsViewModel _results;

        public ResultsViewModel Results
        {
            get => _results;
            private set
            {
                if (value != _results)
                {
                    _results = value;
                    OnPropertyChanged(nameof(Results));
                }
            }
        }

        public ResultsViewModel ContextMenu { get; private set; }

        public ResultsViewModel History { get; private set; }

        private string _systemQueryText = string.Empty;

        public string SystemQueryText
        {
            get => _systemQueryText;
            set
            {
                if (_systemQueryText != value)
                {
                    _systemQueryText = value;
                    OnPropertyChanged(nameof(SystemQueryText));
                }
            }
        }

        private string _queryText = string.Empty;

        public string QueryText
        {
            get => _queryText;

            set
            {
                if (_queryText != value)
                {
                    _queryText = value;
                    OnPropertyChanged(nameof(QueryText));
                }
            }
        }

        /// <summary>
        /// we need move cursor to end when we manually changed query
        /// but we don't want to move cursor to end when query is updated from TextBox.
        /// Also we don't want to force the results to change unless explicitly told to.
        /// </summary>
        /// <param name="queryText">Text that is being queried from user</param>
        /// <param name="requery">Optional Parameter that if true, will automatically execute a query against the updated text</param>
        public void ChangeQueryText(string queryText, bool requery = false)
        {
            SystemQueryText = queryText;

            if (requery)
            {
                QueryText = queryText;
                Query();
            }
        }

        public bool LastQuerySelected { get; set; }

        private ResultsViewModel _selectedResults;

        private ResultsViewModel SelectedResults
        {
            get
            {
                return _selectedResults;
            }

            set
            {
                _selectedResults = value;
                if (SelectedIsFromQueryResults())
                {
                    ContextMenu.Visibility = Visibility.Hidden;
                    History.Visibility = Visibility.Hidden;
                    ChangeQueryText(_queryTextBeforeLeaveResults);
                }
                else
                {
                    Results.Visibility = Visibility.Hidden;
                    _queryTextBeforeLeaveResults = QueryText;
                }

                _selectedResults.Visibility = Visibility.Visible;
            }
        }

        public bool LoadedAtLeastOnce { get; set; }

        private Visibility _visibility;

        public Visibility MainWindowVisibility
        {
            get
            {
                return _visibility;
            }

            set
            {
                if (_visibility != value)
                {
                    _visibility = value;
                    if (LoadedAtLeastOnce)
                    {
                        // Don't trigger telemetry on cold boot. Must have been loaded at least once.
                        if (value == Visibility.Visible)
                        {
                            PowerToysTelemetry.Log.WriteEvent(new LauncherShowEvent());
                        }
                        else
                        {
                            PowerToysTelemetry.Log.WriteEvent(new LauncherHideEvent());
                        }
                    }

                    OnPropertyChanged(nameof(MainWindowVisibility));
                }
            }
        }

        public ICommand IgnoreCommand { get; private set; }

        public ICommand EscCommand { get; private set; }

        public ICommand SelectNextItemCommand { get; private set; }

        public ICommand SelectPrevItemCommand { get; private set; }

        public ICommand SelectNextContextMenuItemCommand { get; private set; }

        public ICommand SelectPreviousContextMenuItemCommand { get; private set; }

        public ICommand SelectNextTabItemCommand { get; private set; }

        public ICommand SelectPrevTabItemCommand { get; private set; }

        public ICommand SelectNextPageCommand { get; private set; }

        public ICommand SelectPrevPageCommand { get; private set; }

        public ICommand OpenResultWithKeyboardCommand { get; private set; }

        public ICommand OpenResultWithMouseCommand { get; private set; }

        public ICommand ClearQueryCommand { get; private set; }

        public class QueryTuningOptions
        {
            public int SearchClickedItemWeight { get; set; }

            public bool SearchQueryTuningEnabled { get; set; }

            public bool SearchWaitForSlowResults { get; set; }
        }

        public void Query()
        {
            Query(null);
        }

        public void Query(bool? delayedExecution)
        {
            if (SelectedIsFromQueryResults())
            {
                QueryResults(delayedExecution);
            }
            else if (HistorySelected())
            {
                QueryHistory();
            }
        }

        private void QueryHistory()
        {
            // Using CurrentCulture since query is received from user and used in downstream comparisons using CurrentCulture
            var query = QueryText.ToLower(CultureInfo.CurrentCulture).Trim();
            History.Clear();

            var results = new List<Result>();
            foreach (var h in _history.Items)
            {
                var title = Properties.Resources.executeQuery;
                var time = Properties.Resources.lastExecuteTime;
                var result = new Result
                {
                    Title = string.Format(CultureInfo.InvariantCulture, title, h.Query),
                    SubTitle = string.Format(CultureInfo.InvariantCulture, time, h.ExecutedDateTime),
                    IcoPath = "Images\\history.png",
                    OriginQuery = new Query(h.Query),
                    Action = _ =>
                    {
                        SelectedResults = Results;
                        ChangeQueryText(h.Query);
                        return false;
                    },
                };
                results.Add(result);
            }

            if (!string.IsNullOrEmpty(query))
            {
                var filtered = results.Where(
                    r => StringMatcher.FuzzySearch(query, r.Title).IsSearchPrecisionScoreMet() ||
                         StringMatcher.FuzzySearch(query, r.SubTitle).IsSearchPrecisionScoreMet()).ToList();

                History.AddResults(filtered, _updateToken);
            }
            else
            {
                History.AddResults(results, _updateToken);
            }
        }

        private void QueryResults()
        {
            QueryResults(null);
        }

        private void QueryResults(bool? delayedExecution)
        {
            var queryTuning = GetQueryTuningOptions();
            var doFinalSort = queryTuning.SearchQueryTuningEnabled && queryTuning.SearchWaitForSlowResults;

            if (!string.IsNullOrEmpty(QueryText))
            {
                var queryTimer = new System.Diagnostics.Stopwatch();
                queryTimer.Start();
                _updateSource?.Cancel();
                var currentUpdateSource = new CancellationTokenSource();
                _updateSource = currentUpdateSource;
                var currentCancellationToken = _updateSource.Token;
                _updateToken = currentCancellationToken;
                var queryText = QueryText.Trim();

                var pluginQueryPairs = QueryBuilder.Build(queryText);
                if (pluginQueryPairs != null && pluginQueryPairs.Count > 0)
                {
                    queryText = pluginQueryPairs.Values.First().RawQuery;
                    _currentQuery = queryText;

                    var queryResultsTask = Task.Factory.StartNew(
                        () =>
                        {
                            Thread.Sleep(20);

                            // Keep track of total number of results for telemetry
                            var numResults = 0;

                            // Contains all the plugins for which this raw query is valid
                            var plugins = pluginQueryPairs.Keys.ToList();

                            var sw = System.Diagnostics.Stopwatch.StartNew();

                            try
                            {
                                var resultPluginPair = new System.Collections.Concurrent.ConcurrentDictionary<PluginMetadata, List<Result>>();

                                if (_settings.PTRunNonDelayedSearchInParallel)
                                {
                                    Parallel.ForEach(pluginQueryPairs, (pluginQueryItem) =>
                                    {
                                        try
                                        {
                                            var plugin = pluginQueryItem.Key;
                                            var query = pluginQueryItem.Value;
                                            query.SelectedItems = _userSelectedRecord.GetGenericHistory();
                                            var results = PluginManager.QueryForPlugin(plugin, query);
                                            resultPluginPair[plugin.Metadata] = results;
                                            currentCancellationToken.ThrowIfCancellationRequested();
                                        }
                                        catch (OperationCanceledException)
                                        {
                                            // nothing to do here
                                        }
                                    });
                                    sw.Stop();
                                }
                                else
                                {
                                    currentCancellationToken.ThrowIfCancellationRequested();

                                    // To execute a query corresponding to each plugin
                                    foreach (KeyValuePair<PluginPair, Query> pluginQueryItem in pluginQueryPairs)
                                    {
                                        var plugin = pluginQueryItem.Key;
                                        var query = pluginQueryItem.Value;
                                        query.SelectedItems = _userSelectedRecord.GetGenericHistory();
                                        var results = PluginManager.QueryForPlugin(plugin, query);
                                        resultPluginPair[plugin.Metadata] = results;
                                        currentCancellationToken.ThrowIfCancellationRequested();
                                    }
                                }

                                lock (_addResultsLock)
                                {
                                    // Using CurrentCultureIgnoreCase since this is user facing
                                    if (queryText.Equals(_currentQuery, StringComparison.CurrentCultureIgnoreCase))
                                    {
                                        Results.Clear();
                                        foreach (var p in resultPluginPair)
                                        {
                                            UpdateResultView(p.Value, queryText, currentCancellationToken);
                                            currentCancellationToken.ThrowIfCancellationRequested();
                                        }

                                        currentCancellationToken.ThrowIfCancellationRequested();
                                        numResults = Results.Results.Count;
                                        if (!doFinalSort)
                                        {
                                            Results.Sort(queryTuning);
                                            Results.SelectedItem = Results.Results.FirstOrDefault();
                                        }
                                    }

                                    currentCancellationToken.ThrowIfCancellationRequested();
                                    if (!doFinalSort)
                                    {
                                        UpdateResultsListViewAfterQuery(queryText);
                                    }
                                }

                                bool noInitialResults = numResults == 0;

                                if (!delayedExecution.HasValue || delayedExecution.Value)
                                {
                                    // Run the slower query of the DelayedExecution plugins
                                    currentCancellationToken.ThrowIfCancellationRequested();
                                    Parallel.ForEach(plugins, (plugin) =>
                                    {
                                        try
                                        {
                                            Query query;
                                            pluginQueryPairs.TryGetValue(plugin, out query);
                                            var results = PluginManager.QueryForPlugin(plugin, query, true);
                                            currentCancellationToken.ThrowIfCancellationRequested();
                                            if ((results?.Count ?? 0) != 0)
                                            {
                                                lock (_addResultsLock)
                                                {
                                                    // Using CurrentCultureIgnoreCase since this is user facing
                                                    if (queryText.Equals(_currentQuery, StringComparison.CurrentCultureIgnoreCase))
                                                    {
                                                        currentCancellationToken.ThrowIfCancellationRequested();

                                                        // Remove the original results from the plugin
                                                        Results.Results.RemoveAll(r => r.Result.PluginID == plugin.Metadata.ID);
                                                        currentCancellationToken.ThrowIfCancellationRequested();

                                                        // Add the new results from the plugin
                                                        UpdateResultView(results, queryText, currentCancellationToken);

                                                        currentCancellationToken.ThrowIfCancellationRequested();
                                                        numResults = Results.Results.Count;
                                                        if (!doFinalSort)
                                                        {
                                                            Results.Sort(queryTuning);
                                                        }
                                                    }

                                                    currentCancellationToken.ThrowIfCancellationRequested();
                                                    if (!doFinalSort)
                                                    {
                                                        UpdateResultsListViewAfterQuery(queryText, noInitialResults, true);
                                                    }
                                                }
                                            }
                                        }
                                        catch (OperationCanceledException)
                                        {
                                            // nothing to do here
                                        }
                                    });
                                }
                            }
                            catch (OperationCanceledException)
                            {
                                // nothing to do here
                            }

                            queryTimer.Stop();
                            var queryEvent = new LauncherQueryEvent()
                            {
                                QueryTimeMs = queryTimer.ElapsedMilliseconds,
                                NumResults = numResults,
                                QueryLength = queryText.Length,
                            };
                            PowerToysTelemetry.Log.WriteEvent(queryEvent);
                        },
                        currentCancellationToken);

                    if (doFinalSort)
                    {
                        Task.Factory.ContinueWhenAll(
                            new Task[] { queryResultsTask },
                            completedTasks =>
                            {
                                Results.Sort(queryTuning);
                                Results.SelectedItem = Results.Results.FirstOrDefault();
                                UpdateResultsListViewAfterQuery(queryText, false, false);
                            },
                            currentCancellationToken);
                    }
                }
            }
            else
            {
                _updateSource?.Cancel();
                _currentQuery = _emptyQuery;
                Results.SelectedItem = null;
                Results.Visibility = Visibility.Hidden;
                Task.Run(() =>
                {
                    lock (_addResultsLock)
                    {
                        Results.Clear();
                    }
                });
            }
        }

        private void UpdateResultsListViewAfterQuery(string queryText, bool noInitialResults = false, bool isDelayedInvoke = false)
        {
            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                // Using CurrentCultureIgnoreCase since this is user facing
                if (queryText.Equals(_currentQuery, StringComparison.CurrentCultureIgnoreCase))
                {
                    Results.Results.NotifyChanges();
                }

                if (Results.Results.Count > 0)
                {
                    Results.Visibility = Visibility.Visible;
                    if (!isDelayedInvoke || noInitialResults)
                    {
                        Results.SelectedIndex = 0;
                        if (noInitialResults)
                        {
                            Results.SelectedItem = Results.Results.FirstOrDefault();
                        }
                    }
                }
                else
                {
                    Results.Visibility = Visibility.Hidden;
                }
            }));
        }

        private bool SelectedIsFromQueryResults()
        {
            var selected = SelectedResults == Results;
            return selected;
        }

        private bool HistorySelected()
        {
            var selected = SelectedResults == History;
            return selected;
        }

        internal bool ProcessHotKeyMessages(IntPtr wparam, IntPtr lparam)
        {
            if (wparam.ToInt32() == _globalHotKeyId)
            {
                OnHotkey();
                return true;
            }

            return false;
        }

        private static uint VKModifiersFromHotKey(Hotkey hotkey)
        {
            return (uint)(HOTKEY_MODIFIERS.NOREPEAT | (hotkey.Alt ? HOTKEY_MODIFIERS.ALT : 0) | (hotkey.Ctrl ? HOTKEY_MODIFIERS.CONTROL : 0) | (hotkey.Shift ? HOTKEY_MODIFIERS.SHIFT : 0) | (hotkey.Win ? HOTKEY_MODIFIERS.WIN : 0));
        }

        private void SetHotkey(IntPtr hwnd, string hotkeyStr, HotkeyCallback action)
        {
            var hotkey = new HotkeyModel(hotkeyStr);
            SetHotkey(hwnd, hotkey, action);
        }

        private void SetHotkey(IntPtr hwnd, HotkeyModel hotkeyModel, HotkeyCallback action)
        {
            Log.Info("Set HotKey()", GetType());
            string hotkeyStr = hotkeyModel.ToString();

            try
            {
                Hotkey hotkey = new Hotkey
                {
                    Alt = hotkeyModel.Alt,
                    Shift = hotkeyModel.Shift,
                    Ctrl = hotkeyModel.Ctrl,
                    Win = hotkeyModel.Win,
                    Key = (byte)KeyInterop.VirtualKeyFromKey(hotkeyModel.CharKey),
                };

                if (_usingGlobalHotKey)
                {
                    NativeMethods.UnregisterHotKey(_globalHotKeyHwnd, _globalHotKeyId);
                    _usingGlobalHotKey = false;
                    Log.Info("Unregistering previous global hotkey", GetType());
                }

                if (_hotkeyHandle != 0)
                {
                    HotkeyManager?.UnregisterHotkey(_hotkeyHandle);
                    _hotkeyHandle = 0;
                    Log.Info("Unregistering previous low level key handler", GetType());
                }

                if (_settings.StartedFromPowerToysRunner && _settings.UseCentralizedKeyboardHook)
                {
                    Log.Info("Using the Centralized Keyboard Hook for the HotKey.", GetType());
                }
                else
                {
                    _globalHotKeyVK = hotkey.Key;
                    _globalHotKeyFSModifiers = VKModifiersFromHotKey(hotkey);
                    if (NativeMethods.RegisterHotKey(hwnd, _globalHotKeyId, _globalHotKeyFSModifiers, _globalHotKeyVK))
                    {
                        // Using global hotkey registered through the native RegisterHotKey method.
                        _globalHotKeyHwnd = hwnd;
                        _usingGlobalHotKey = true;
                        Log.Info("Registered global hotkey", GetType());
                        return;
                    }

                    Log.Warn("Registering global shortcut failed. Will use low-level keyboard hook instead.", GetType());

                    // Using fallback low-level keyboard hook through HotkeyManager.
                    if (HotkeyManager == null)
                    {
                        HotkeyManager = new HotkeyManager();
                    }

                    _hotkeyHandle = HotkeyManager.RegisterHotkey(hotkey, action);
                }
            }
            catch (Exception)
            {
                string errorMsg = string.Format(CultureInfo.InvariantCulture, Properties.Resources.registerHotkeyFailed, hotkeyStr);
                MessageBox.Show(errorMsg);
            }
        }

        /// <summary>
        /// Checks if Wox should ignore any hotkeys
        /// </summary>
        /// <returns>if any hotkeys should be ignored</returns>
        private bool ShouldIgnoreHotkeys()
        {
            // double if to omit calling win32 function
            if (_settings.IgnoreHotkeysOnFullscreen)
            {
                if (WindowsInteropHelper.IsWindowFullscreen())
                {
                    return true;
                }
            }

            return false;
        }

        /* TODO: Custom Hotkeys for Plugins. Commented since this is an incomplete feature.
         * This needs:
         *  - Support for use with global shortcut.
         *  - Support for use with the fallback Shortcut Manager.
         *  - Support for use through the runner centralized keyboard hooks.
        private void SetCustomPluginHotkey()
        {
            if (_settings.CustomPluginHotkeys == null)
            {
                return;
            }

            foreach (CustomPluginHotkey hotkey in _settings.CustomPluginHotkeys)
            {
                SetHotkey(hotkey.Hotkey, () =>
                {
                    if (ShouldIgnoreHotkeys())
                    {
                        return;
                    }

                    MainWindowVisibility = Visibility.Visible;
                    ChangeQueryText(hotkey.ActionKeyword);
                });
            }
        }
        */

        private void OnCentralizedKeyboardHookHotKey()
        {
            if (_settings.StartedFromPowerToysRunner && _settings.UseCentralizedKeyboardHook)
            {
                OnHotkey();
            }
        }

        private void OnHotkey()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                Log.Info("OnHotkey", MethodBase.GetCurrentMethod().DeclaringType);
                if (!ShouldIgnoreHotkeys())
                {
                    // If launcher window was hidden and the hotkey was pressed, start telemetry event
                    if (MainWindowVisibility != Visibility.Visible)
                    {
                        StartHotkeyTimer();
                    }

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
                }
            });
        }

        public void ToggleWox()
        {
            if (MainWindowVisibility != Visibility.Visible)
            {
                MainWindowVisibility = Visibility.Visible;
            }
            else
            {
                if (_settings.ClearInputOnLaunch)
                {
                    ClearQueryCommand.Execute(null);
                    Task.Run(() =>
                    {
                        Thread.Sleep(100);
                        Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(() =>
                        {
                            MainWindowVisibility = Visibility.Collapsed;
                        }));
                    });
                }
                else
                {
                    MainWindowVisibility = Visibility.Collapsed;
                }
            }
        }

        public void Hide()
        {
            if (MainWindowVisibility != Visibility.Collapsed)
            {
                ToggleWox();
            }
        }

        public void Save()
        {
            if (!_saved)
            {
                _historyItemsStorage.Save();
                _userSelectedRecordStorage.Save();

                _saved = true;
            }
        }

        /// <summary>
        /// To avoid deadlock, this method should not called from main thread
        /// </summary>
        public void UpdateResultView(List<Result> list, string originQuery, CancellationToken ct)
        {
            if (list == null)
            {
                throw new ArgumentNullException(nameof(list));
            }

            if (originQuery == null)
            {
                throw new ArgumentNullException(nameof(originQuery));
            }

            foreach (var result in list)
            {
                var selectedData = _userSelectedRecord.GetSelectedData(result);
                result.SelectedCount = selectedData.SelectedCount;
                result.LastSelected = selectedData.LastSelected;
            }

            // Using CurrentCultureIgnoreCase since this is user facing
            if (originQuery.Equals(_currentQuery, StringComparison.CurrentCultureIgnoreCase))
            {
                ct.ThrowIfCancellationRequested();
                Results.AddResults(list, ct);
            }
        }

        public void ColdStartFix()
        {
            // Fix Cold start for List view xaml island
            List<Result> list = new List<Result>();
            Result r = new Result
            {
                Title = "hello",
            };
            list.Add(r);
            Results.AddResults(list, _updateToken);
            Results.Clear();

            // Fix Cold start for plugins, "m" is just a random string needed to query results
            var pluginQueryPairs = QueryBuilder.Build("m");

            // To execute a query corresponding to each plugin
            foreach (KeyValuePair<PluginPair, Query> pluginQueryItem in pluginQueryPairs)
            {
                var plugin = pluginQueryItem.Key;
                var query = pluginQueryItem.Value;

                if (!plugin.Metadata.Disabled && plugin.Metadata.Name != "Window Walker")
                {
                    _ = PluginManager.QueryForPlugin(plugin, query);
                }
            }
        }

        public void HandleContextMenu(Key acceleratorKey, ModifierKeys acceleratorModifiers)
        {
            var results = SelectedResults;
            if (results.SelectedItem != null)
            {
                foreach (ContextMenuItemViewModel contextMenuItems in results.SelectedItem.ContextMenuItems)
                {
                    if (contextMenuItems.AcceleratorKey == acceleratorKey && contextMenuItems.AcceleratorModifiers == acceleratorModifiers)
                    {
                        MainWindowVisibility = Visibility.Collapsed;
                        contextMenuItems.Command.Execute(null);
                    }
                }
            }
        }

        public static bool ShouldAutoCompleteTextBeEmpty(string queryText, string autoCompleteText)
        {
            if (string.IsNullOrEmpty(autoCompleteText))
            {
                return false;
            }
            else
            {
                // Using Ordinal this is internal
                return string.IsNullOrEmpty(queryText) || autoCompleteText.IndexOf(queryText, StringComparison.Ordinal) != 0;
            }
        }

        public static string GetAutoCompleteText(int index, string input, string query)
        {
            if (!string.IsNullOrEmpty(input) && !string.IsNullOrEmpty(query))
            {
                if (index == 0)
                {
                    // Using OrdinalIgnoreCase because we want the characters to be exact in autocomplete text and the query
                    if (input.IndexOf(query, StringComparison.OrdinalIgnoreCase) == 0)
                    {
                        // Use the same case as the input query for the matched portion of the string
                        return string.Concat(query, input.AsSpan(query.Length));
                    }
                }
            }

            return string.Empty;
        }

        public static string GetSearchText(int index, string input, string query)
        {
            if (!string.IsNullOrEmpty(input))
            {
                if (index == 0 && !string.IsNullOrEmpty(query))
                {
                    // Using OrdinalIgnoreCase since this is internal
                    if (input.IndexOf(query, StringComparison.OrdinalIgnoreCase) == 0)
                    {
                        return string.Concat(query, input.AsSpan(query.Length));
                    }
                }

                return input;
            }

            return string.Empty;
        }

        public static FlowDirection GetLanguageFlowDirection()
        {
            bool isCurrentLanguageRightToLeft = System.Windows.Input.InputLanguageManager.Current.CurrentInputLanguage.TextInfo.IsRightToLeft;

            if (isCurrentLanguageRightToLeft)
            {
                return FlowDirection.RightToLeft;
            }
            else
            {
                return FlowDirection.LeftToRight;
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    if (_usingGlobalHotKey)
                    {
                        NativeMethods.UnregisterHotKey(_globalHotKeyHwnd, _globalHotKeyId);
                        _usingGlobalHotKey = false;
                    }

                    if (_hotkeyHandle != 0)
                    {
                        HotkeyManager?.UnregisterHotkey(_hotkeyHandle);
                    }

                    HotkeyManager?.Dispose();
                    _updateSource?.Dispose();
                    _disposed = true;
                }
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        public void StartHotkeyTimer()
        {
            _hotkeyTimer.Start();
        }

        public long GetHotkeyEventTimeMs()
        {
            _hotkeyTimer.Stop();
            long recordedTime = _hotkeyTimer.ElapsedMilliseconds;

            // Reset the stopwatch and return the time elapsed
            _hotkeyTimer.Reset();
            return recordedTime;
        }

        public bool GetSearchQueryResultsWithDelaySetting()
        {
            return _settings.SearchQueryResultsWithDelay;
        }

        public int GetSearchInputDelayFastSetting()
        {
            return _settings.SearchInputDelayFast;
        }

        public int GetSearchInputDelaySetting()
        {
            return _settings.SearchInputDelay;
        }

        public QueryTuningOptions GetQueryTuningOptions()
        {
            return new MainViewModel.QueryTuningOptions
            {
                SearchClickedItemWeight = GetSearchClickedItemWeight(),
                SearchQueryTuningEnabled = GetSearchQueryTuningEnabled(),
                SearchWaitForSlowResults = GetSearchWaitForSlowResults(),
            };
        }

        public int GetSearchClickedItemWeight()
        {
            return _settings.SearchClickedItemWeight;
        }

        public bool GetSearchQueryTuningEnabled()
        {
            return _settings.SearchQueryTuningEnabled;
        }

        public bool GetSearchWaitForSlowResults()
        {
            return _settings.SearchWaitForSlowResults;
        }
    }
}
