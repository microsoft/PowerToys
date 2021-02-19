// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
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
    public class MainViewModel : BaseModel, ISavable, IDisposable
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
        private bool _saved;
        private ushort _hotkeyHandle;

        internal HotkeyManager HotkeyManager { get; set; }

        public MainViewModel(PowerToysRunSettings settings)
        {
            _saved = false;
            _queryTextBeforeLeaveResults = string.Empty;
            _currentQuery = _emptyQuery;
            _disposed = false;

            _settings = settings ?? throw new ArgumentNullException(nameof(settings));

            _historyItemsStorage = new WoxJsonStorage<QueryHistory>();
            _userSelectedRecordStorage = new WoxJsonStorage<UserSelectedRecord>();
            _history = _historyItemsStorage.Load();
            _userSelectedRecord = _userSelectedRecordStorage.Load();

            ContextMenu = new ResultsViewModel(_settings);
            Results = new ResultsViewModel(_settings);
            History = new ResultsViewModel(_settings);
            _selectedResults = Results;

            InitializeKeyCommands();
            RegisterResultsUpdatedEvent();

            if (settings != null && settings.UsePowerToysRunnerKeyboardHook)
            {
                NativeEventWaiter.WaitForEventLoop(Constants.PowerLauncherSharedEvent(), OnHotkey);
                _hotkeyHandle = 0;
            }
            else
            {
                HotkeyManager = new HotkeyManager();
                _settings.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(PowerToysRunSettings.Hotkey))
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            if (!string.IsNullOrEmpty(_settings.PreviousHotkey))
                            {
                                HotkeyManager.UnregisterHotkey(_hotkeyHandle);
                            }

                            if (!string.IsNullOrEmpty(_settings.Hotkey))
                            {
                                SetHotkey(_settings.Hotkey, OnHotkey);
                            }
                        });
                    }
                };

                SetHotkey(_settings.Hotkey, OnHotkey);
                SetCustomPluginHotkey();
            }
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
                    }, _updateToken);
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
                        MainWindowVisibility = Visibility.Collapsed;

                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            result.Action(new ActionContext
                            {
                                SpecialKeyState = KeyboardHelper.CheckModifiers(),
                            });
                        });

                        if (SelectedIsFromQueryResults())
                        {
                            // todo: revert _userSelectedRecordStorage.Save() and _historyItemsStorage.Save() after https://github.com/microsoft/PowerToys/issues/9164 is done
                            _userSelectedRecord.Add(result);
                            _userSelectedRecordStorage.Save();
                            _history.Add(result.OriginQuery.RawQuery);
                            _historyItemsStorage.Save();
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

            SelectFirstResultCommand = new RelayCommand(_ => SelectedResults.SelectFirstResult());

            StartHelpCommand = new RelayCommand(_ =>
            {
                Process.Start("https://aka.ms/PowerToys/");
            });

            OpenResultWithKeyboardCommand = new RelayCommand(index =>
            {
                OpenResultsEvent(index, false);
            });

            OpenResultWithMouseCommand = new RelayCommand(index =>
            {
                OpenResultsEvent(index, true);
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

        public Brush MainWindowBackground { get; set; }

        public Brush MainWindowBorderBrush { get; set; }

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
                    OnPropertyChanged(nameof(MainWindowVisibility));
                }

                if (value == Visibility.Visible)
                {
                    PowerToysTelemetry.Log.WriteEvent(new LauncherShowEvent());
                }
                else
                {
                    PowerToysTelemetry.Log.WriteEvent(new LauncherHideEvent());
                }
            }
        }

        public ICommand IgnoreCommand { get; set; }

        public ICommand EscCommand { get; set; }

        public ICommand SelectNextItemCommand { get; set; }

        public ICommand SelectPrevItemCommand { get; set; }

        public ICommand SelectNextContextMenuItemCommand { get; set; }

        public ICommand SelectPreviousContextMenuItemCommand { get; set; }

        public ICommand SelectNextTabItemCommand { get; set; }

        public ICommand SelectPrevTabItemCommand { get; set; }

        public ICommand SelectNextPageCommand { get; set; }

        public ICommand SelectPrevPageCommand { get; set; }

        public ICommand SelectFirstResultCommand { get; set; }

        public ICommand StartHelpCommand { get; set; }

        public ICommand LoadContextMenuCommand { get; set; }

        public ICommand LoadHistoryCommand { get; set; }

        public ICommand OpenResultWithKeyboardCommand { get; set; }

        public ICommand OpenResultWithMouseCommand { get; set; }

        public ICommand ClearQueryCommand { get; set; }

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
            // Using CurrentCulture since query is received from user and used in downstream comparisons using CurrentCulture
#pragma warning disable CA1308 // Normalize strings to uppercase
            var query = QueryText.ToLower(CultureInfo.CurrentCulture).Trim();
#pragma warning restore CA1308 // Normalize strings to uppercase
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
                    Task.Run(
                        () =>
                    {
                        Thread.Sleep(20);

                        // Keep track of total number of results for telemetry
                        var numResults = 0;

                        // Contains all the plugins for which this raw query is valid
                        var plugins = pluginQueryPairs.Keys.ToList();

                        try
                        {
                            currentCancellationToken.ThrowIfCancellationRequested();

                            var resultPluginPair = new List<(List<Result>, PluginMetadata)>();

                            // To execute a query corresponding to each plugin
                            foreach (KeyValuePair<PluginPair, Query> pluginQueryItem in pluginQueryPairs)
                            {
                                var plugin = pluginQueryItem.Key;
                                var query = pluginQueryItem.Value;
                                var results = PluginManager.QueryForPlugin(plugin, query);
                                resultPluginPair.Add((results, plugin.Metadata));
                                currentCancellationToken.ThrowIfCancellationRequested();
                            }

                            lock (_addResultsLock)
                            {
                                // Using CurrentCultureIgnoreCase since this is user facing
                                if (queryText.Equals(_currentQuery, StringComparison.CurrentCultureIgnoreCase))
                                {
                                    Results.Clear();
                                    foreach (var p in resultPluginPair)
                                    {
                                        UpdateResultView(p.Item1, queryText, currentCancellationToken);
                                        currentCancellationToken.ThrowIfCancellationRequested();
                                    }

                                    currentCancellationToken.ThrowIfCancellationRequested();
                                    numResults = Results.Results.Count;
                                    Results.Sort();
                                    Results.SelectedItem = Results.Results.FirstOrDefault();
                                }
                            }

                            currentCancellationToken.ThrowIfCancellationRequested();

                            UpdateResultsListViewAfterQuery(queryText);

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
                                                    Results.Sort();
                                                    Results.SelectedItem = Results.Results.FirstOrDefault();
                                                }
                                            }

                                            currentCancellationToken.ThrowIfCancellationRequested();
                                            UpdateResultsListViewAfterQuery(queryText, true);
                                        }
                                    }
                                    catch (OperationCanceledException)
                                    {
                                        // nothing to do here
                                    }
                                });
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
                    }, currentCancellationToken);
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

        private void UpdateResultsListViewAfterQuery(string queryText, bool isDelayedInvoke = false)
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
                    if (!isDelayedInvoke)
                    {
                        Results.SelectedIndex = 0;
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

        private void SetHotkey(string hotkeyStr, HotkeyCallback action)
        {
            var hotkey = new HotkeyModel(hotkeyStr);
            SetHotkey(hotkey, action);
        }

        private void SetHotkey(HotkeyModel hotkeyModel, HotkeyCallback action)
        {
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

                _hotkeyHandle = HotkeyManager.RegisterHotkey(hotkey, action);
            }
#pragma warning disable CA1031 // Do not catch general exception types
            catch (Exception)
#pragma warning restore CA1031 // Do not catch general exception types
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
                result.Score += _userSelectedRecord.GetSelectedCount(result) * 5;
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
            MainWindowVisibility = System.Windows.Visibility.Collapsed;

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
                        return query + input.Substring(query.Length);
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
                        return query + input.Substring(query.Length);
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
    }
}
