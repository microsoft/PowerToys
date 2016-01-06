using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media.Animation;
using NHotkey;
using NHotkey.Wpf;
using Wox.Core.i18n;
using Wox.Core.Plugin;
using Wox.Core.Theme;
using Wox.Core.Updater;
using Wox.Core.UserSettings;
using Wox.Helper;
using Wox.Infrastructure;
using Wox.Infrastructure.Hotkey;
using Wox.Plugin;
using Wox.Storage;
using Application = System.Windows.Application;
using ContextMenu = System.Windows.Forms.ContextMenu;
using DataFormats = System.Windows.DataFormats;
using DragEventArgs = System.Windows.DragEventArgs;
using IDataObject = System.Windows.IDataObject;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using MenuItem = System.Windows.Forms.MenuItem;
using MessageBox = System.Windows.MessageBox;
using Stopwatch = Wox.Infrastructure.Stopwatch;
using ToolTip = System.Windows.Controls.ToolTip;

namespace Wox
{
    public partial class MainWindow : IPublicAPI
    {

        #region Properties

        private readonly Storyboard progressBarStoryboard = new Storyboard();
        private NotifyIcon notifyIcon;
        private bool _queryHasReturn;
        private Query _lastQuery = new Query();
        private ToolTip toolTip = new ToolTip();

        private bool _ignoreTextChange;
        private List<Result> CurrentContextMenus = new List<Result>();
        private string textBeforeEnterContextMenuMode;

        #endregion

        #region Public API

        public void ChangeQuery(string query, bool requery = false)
        {
            Dispatcher.Invoke(() =>
            {
                tbQuery.Text = query;
                tbQuery.CaretIndex = tbQuery.Text.Length;
                if (requery)
                {
                    TbQuery_OnTextChanged(null, null);
                }
            });
        }

        public void ChangeQueryText(string query, bool selectAll = false)
        {
            Dispatcher.Invoke(() =>
            {
                _ignoreTextChange = true;
                tbQuery.Text = query;
                tbQuery.CaretIndex = tbQuery.Text.Length;
                if (selectAll)
                {
                    tbQuery.SelectAll();
                }
            });
        }

        public void CloseApp()
        {
            notifyIcon.Visible = false;
            Application.Current.Shutdown();
        }

        public void RestarApp()
        {
            ProcessStartInfo info = new ProcessStartInfo
            {
                FileName = Application.ResourceAssembly.Location,
                Arguments = SingleInstance<App>.Restart
            };
            Process.Start(info);
        }

        public void HideApp()
        {
            Dispatcher.Invoke(HideWox);
        }

        public void ShowApp()
        {
            Dispatcher.Invoke(() => ShowWox());
        }

        public void ShowMsg(string title, string subTitle, string iconPath)
        {
            Dispatcher.Invoke(() =>
            {
                var m = new Msg { Owner = GetWindow(this) };
                m.Show(title, subTitle, iconPath);
            });
        }

        public void OpenSettingDialog(string tabName = "general")
        {
            Dispatcher.Invoke(() =>
            {
                SettingWindow sw = SingletonWindowOpener.Open<SettingWindow>(this);
                sw.SwitchTo(tabName);
            });
        }

        public void StartLoadingBar()
        {
            Dispatcher.Invoke(StartProgress);
        }

        public void StopLoadingBar()
        {
            Dispatcher.Invoke(StopProgress);
        }

        public void InstallPlugin(string path)
        {
            Dispatcher.Invoke(() => PluginManager.InstallPlugin(path));
        }

        public void ReloadPlugins()
        {
            Dispatcher.Invoke(() => PluginManager.Init(this));
        }

        public string GetTranslation(string key)
        {
            return InternationalizationManager.Instance.GetTranslation(key);
        }

        public List<PluginPair> GetAllPlugins()
        {
            return PluginManager.AllPlugins.ToList();
        }

        public event WoxKeyDownEventHandler BackKeyDownEvent;
        public event WoxGlobalKeyboardEventHandler GlobalKeyboardEvent;
        public event ResultItemDropEventHandler ResultItemDropEvent;

        public void PushResults(Query query, PluginMetadata plugin, List<Result> results)
        {
            results.ForEach(o =>
            {
                o.PluginDirectory = plugin.PluginDirectory;
                o.PluginID = plugin.ID;
                o.OriginQuery = query;
            });
            UpdateResultView(results, plugin, query);
        }

        public void ShowContextMenu(PluginMetadata plugin, List<Result> results)
        {
            if (results != null && results.Count > 0)
            {
                results.ForEach(o =>
                {
                    o.PluginDirectory = plugin.PluginDirectory;
                    o.PluginID = plugin.ID;
                    o.ContextMenu = null;
                });
                pnlContextMenu.Clear();
                pnlContextMenu.AddResults(results, plugin.ID);
                pnlContextMenu.Visibility = Visibility.Visible;
                pnlResult.Visibility = Visibility.Collapsed;
            }
        }

        #endregion

        public MainWindow()
        {
            InitializeComponent();
            ThreadPool.SetMaxThreads(30, 10);
            ThreadPool.SetMinThreads(10, 5);

            WebRequest.RegisterPrefix("data", new DataWebRequestFactory());
            GlobalHotkey.Instance.hookedKeyboardCallback += KListener_hookedKeyboardCallback;
            progressBar.ToolTip = toolTip;
            pnlResult.LeftMouseClickEvent += SelectResult;
            pnlResult.ItemDropEvent += pnlResult_ItemDropEvent;
            pnlContextMenu.LeftMouseClickEvent += SelectResult;
            pnlResult.RightMouseClickEvent += pnlResult_RightMouseClickEvent;
            Closing += MainWindow_Closing;


            SetHotkey(UserSettingStorage.Instance.Hotkey, OnHotkey);
            SetCustomPluginHotkey();
            InitialTray();
        }

        void pnlResult_ItemDropEvent(Result result, IDataObject dropDataObject, DragEventArgs args)
        {
            PluginPair pluginPair = PluginManager.AllPlugins.FirstOrDefault(o => o.Metadata.ID == result.PluginID);
            if (ResultItemDropEvent != null && pluginPair != null)
            {
                foreach (var delegateHandler in ResultItemDropEvent.GetInvocationList())
                {
                    if (delegateHandler.Target == pluginPair.Plugin)
                    {
                        delegateHandler.DynamicInvoke(result, dropDataObject, args);
                    }
                }
            }
        }

        private bool KListener_hookedKeyboardCallback(KeyEvent keyevent, int vkcode, SpecialKeyState state)
        {
            if (GlobalKeyboardEvent != null)
            {
                return GlobalKeyboardEvent((int)keyevent, vkcode, state);
            }
            return true;
        }

        void pnlResult_RightMouseClickEvent(Result result)
        {
            ShowContextMenu(result);
        }

        void MainWindow_Closing(object sender, CancelEventArgs e)
        {
            UserSettingStorage.Instance.WindowLeft = Left;
            UserSettingStorage.Instance.WindowTop = Top;
            UserSettingStorage.Instance.Save();
            HideWox();
            e.Cancel = true;
        }

        private void MainWindow_OnLoaded(object sender, RoutedEventArgs e)
        {
            ThemeManager.Theme.ChangeTheme(UserSettingStorage.Instance.Theme);
            InternationalizationManager.Instance.ChangeLanguage(UserSettingStorage.Instance.Language);

            Left = GetWindowsLeft();
            Top = GetWindowsTop();

            InitProgressbarAnimation();
            WindowIntelopHelper.DisableControlBox(this);
            CheckUpdate();
        }

        private double GetWindowsLeft()
        {
            if (UserSettingStorage.Instance.RememberLastLaunchLocation) return UserSettingStorage.Instance.WindowLeft;

            var screen = Screen.FromPoint(System.Windows.Forms.Cursor.Position);
            var dipPoint = WindowIntelopHelper.TransformPixelsToDIP(this, screen.WorkingArea.Width, 0);
            UserSettingStorage.Instance.WindowLeft = (dipPoint.X - ActualWidth)/2;
            return UserSettingStorage.Instance.WindowLeft;
        }

        private double GetWindowsTop()
        {
            if (UserSettingStorage.Instance.RememberLastLaunchLocation) return UserSettingStorage.Instance.WindowTop;

            var screen = Screen.FromPoint(System.Windows.Forms.Cursor.Position);
            var dipPoint = WindowIntelopHelper.TransformPixelsToDIP(this, 0, screen.WorkingArea.Height);
            UserSettingStorage.Instance.WindowTop = (dipPoint.Y - tbQuery.ActualHeight)/4;
            return UserSettingStorage.Instance.WindowTop;
        }

        private void CheckUpdate()
        {
            UpdaterManager.Instance.PrepareUpdateReady += OnPrepareUpdateReady;
            UpdaterManager.Instance.UpdateError += OnUpdateError;
            UpdaterManager.Instance.CheckUpdate();
        }

        void OnUpdateError(object sender, EventArgs e)
        {
            string updateError = InternationalizationManager.Instance.GetTranslation("update_wox_update_error");
            MessageBox.Show(updateError);
        }

        private void OnPrepareUpdateReady(object sender, EventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                new WoxUpdate().ShowDialog();
            });
        }

        public void SetHotkey(string hotkeyStr, EventHandler<HotkeyEventArgs> action)
        {
            var hotkey = new HotkeyModel(hotkeyStr);
            SetHotkey(hotkey, action);
        }

        public void SetHotkey(HotkeyModel hotkey, EventHandler<HotkeyEventArgs> action)
        {
            string hotkeyStr = hotkey.ToString();
            try
            {
                HotkeyManager.Current.AddOrReplace(hotkeyStr, hotkey.CharKey, hotkey.ModifierKeys, action);
            }
            catch (Exception)
            {
                string errorMsg = string.Format(InternationalizationManager.Instance.GetTranslation("registerHotkeyFailed"), hotkeyStr);
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
            if (UserSettingStorage.Instance.IgnoreHotkeysOnFullscreen)
                if (WindowIntelopHelper.IsWindowFullscreen())
                    return true;

            return false;
        }

        private void SetCustomPluginHotkey()
        {
            if (UserSettingStorage.Instance.CustomPluginHotkeys == null) return;
            foreach (CustomPluginHotkey hotkey in UserSettingStorage.Instance.CustomPluginHotkeys)
            {
                CustomPluginHotkey hotkey1 = hotkey;
                SetHotkey(hotkey.Hotkey, delegate
                {
                    if (ShouldIgnoreHotkeys()) return;
                    ShowApp();
                    ChangeQuery(hotkey1.ActionKeyword, true);
                });
            }
        }

        private void OnHotkey(object sender, HotkeyEventArgs e)
        {
            if (ShouldIgnoreHotkeys()) return;
            ToggleWox();
            e.Handled = true;
        }

        public void ToggleWox()
        {
            if (!IsVisible)
            {
                ShowWox();
            }
            else
            {
                HideWox();
            }
        }

        private void InitProgressbarAnimation()
        {
            var da = new DoubleAnimation(progressBar.X2, ActualWidth + 100, new Duration(new TimeSpan(0, 0, 0, 0, 1600)));
            var da1 = new DoubleAnimation(progressBar.X1, ActualWidth, new Duration(new TimeSpan(0, 0, 0, 0, 1600)));
            Storyboard.SetTargetProperty(da, new PropertyPath("(Line.X2)"));
            Storyboard.SetTargetProperty(da1, new PropertyPath("(Line.X1)"));
            progressBarStoryboard.Children.Add(da);
            progressBarStoryboard.Children.Add(da1);
            progressBarStoryboard.RepeatBehavior = RepeatBehavior.Forever;
            progressBar.Visibility = Visibility.Hidden;
            progressBar.BeginStoryboard(progressBarStoryboard);
        }

        private void InitialTray()
        {
            notifyIcon = new NotifyIcon { Text = "Wox", Icon = Properties.Resources.app, Visible = true };
            notifyIcon.Click += (o, e) => ShowWox();
            var open = new MenuItem(GetTranslation("iconTrayOpen"));
            open.Click += (o, e) => ShowWox();
            var setting = new MenuItem(GetTranslation("iconTraySettings"));
            setting.Click += (o, e) => OpenSettingDialog();
            var about = new MenuItem(GetTranslation("iconTrayAbout"));
            about.Click += (o, e) => OpenSettingDialog("about");
            var exit = new MenuItem(GetTranslation("iconTrayExit"));
            exit.Click += (o, e) => CloseApp();
            MenuItem[] childen = { open, setting, about, exit };
            notifyIcon.ContextMenu = new ContextMenu(childen);
        }

        private void QueryContextMenu()
        {
            var contextMenuId = "Context Menu Id";
            pnlContextMenu.Clear();
            var query = tbQuery.Text.ToLower();
            if (string.IsNullOrEmpty(query))
            {
                pnlContextMenu.AddResults(CurrentContextMenus, contextMenuId);
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
                pnlContextMenu.AddResults(filterResults, contextMenuId);
            }
        }

        private void TbQuery_OnTextChanged(object sender, TextChangedEventArgs e)
        {
            if (_ignoreTextChange) { _ignoreTextChange = false; return; }

            toolTip.IsOpen = false;
            if (IsInContextMenuMode)
            {
                QueryContextMenu();
                return;
            }

            string query = tbQuery.Text.Trim();
            if (!string.IsNullOrEmpty(query))
            {
                Query(query);
                //reset query history index after user start new query
                ResetQueryHistoryIndex();
            }
            else
            {
                pnlResult.Clear();
            }
        }

        private void ResetQueryHistoryIndex()
        {
            pnlResult.RemoveResultsFor(QueryHistoryStorage.MetaData);
            QueryHistoryStorage.Instance.Reset();
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
                        pnlResult.RemoveResultsExcept(PluginManager.NonGlobalPlugins[keyword].Metadata);
                    }
                }
                else
                {
                    if (string.IsNullOrEmpty(keyword))
                    {
                        pnlResult.RemoveResultsFor(PluginManager.NonGlobalPlugins[lastKeyword].Metadata);
                    }
                    else if (lastKeyword != keyword)
                    {
                        pnlResult.RemoveResultsExcept(PluginManager.NonGlobalPlugins[keyword].Metadata);
                    }
                }
                _lastQuery = query;
                Dispatcher.InvokeAsync(async () =>
                {
                    await Task.Delay(150);
                    if (!string.IsNullOrEmpty(query.RawQuery) && query.RawQuery == _lastQuery.RawQuery && !_queryHasReturn)
                    {
                        StartProgress();
                    }
                });
                PluginManager.QueryForAllPlugins(query);
            }
            StopProgress();
        }

        private void BackToResultMode()
        {
            ChangeQueryText(textBeforeEnterContextMenuMode);
            pnlResult.Visibility = Visibility.Visible;
            pnlContextMenu.Visibility = Visibility.Collapsed;
        }

        private void Border_OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left) DragMove();
        }

        private void StartProgress()
        {
            progressBar.Visibility = Visibility.Visible;
        }

        private void StopProgress()
        {
            progressBar.Visibility = Visibility.Hidden;
        }

        private void HideWox()
        {
            UserSettingStorage.Instance.WindowLeft = Left;
            UserSettingStorage.Instance.WindowTop = Top;
            if (IsInContextMenuMode)
            {
                BackToResultMode();
            }
            Hide();
        }

        private void ShowWox(bool selectAll = true)
        {
            UserSettingStorage.Instance.IncreaseActivateTimes();
            Left = GetWindowsLeft();
            Top = GetWindowsTop();

            Show();
            Activate();
            Focus();
            tbQuery.Focus();
            ResetQueryHistoryIndex();
            if (selectAll) tbQuery.SelectAll();
        }

        private void MainWindow_OnDeactivated(object sender, EventArgs e)
        {
            if (UserSettingStorage.Instance.HideWhenDeactive)
            {
                HideWox();
            }
        }

        private void TbQuery_OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            //when alt is pressed, the real key should be e.SystemKey
            Key key = (e.Key == Key.System ? e.SystemKey : e.Key);
            switch (key)
            {
                case Key.Escape:
                    if (IsInContextMenuMode)
                    {
                        BackToResultMode();
                    }
                    else
                    {
                        HideWox();
                    }
                    e.Handled = true;
                    break;

                case Key.Tab:
                    if (GlobalHotkey.Instance.CheckModifiers().ShiftPressed)
                    {
                        SelectPrevItem();
                    }
                    else
                    {
                        SelectNextItem();
                    }
                    e.Handled = true;
                    break;

                case Key.N:
                case Key.J:
                    if (GlobalHotkey.Instance.CheckModifiers().CtrlPressed)
                    {
                        SelectNextItem();
                    }
                    break;

                case Key.P:
                case Key.K:
                    if (GlobalHotkey.Instance.CheckModifiers().CtrlPressed)
                    {
                        SelectPrevItem();
                    }
                    break;

                case Key.O:
                    if (GlobalHotkey.Instance.CheckModifiers().CtrlPressed)
                    {
                        if (IsInContextMenuMode)
                        {
                            BackToResultMode();
                        }
                        else
                        {
                            ShowContextMenu(GetActiveResult());
                        }
                    }
                    break;

                case Key.Down:
                    if (GlobalHotkey.Instance.CheckModifiers().CtrlPressed)
                    {
                        DisplayNextQuery();
                    }
                    else
                    {
                        SelectNextItem();
                    }
                    e.Handled = true;
                    break;

                case Key.Up:
                    if (GlobalHotkey.Instance.CheckModifiers().CtrlPressed)
                    {
                        DisplayPrevQuery();
                    }
                    else
                    {
                        SelectPrevItem();
                    }
                    e.Handled = true;
                    break;

                case Key.D:
                    if (GlobalHotkey.Instance.CheckModifiers().CtrlPressed)
                    {
                        pnlResult.SelectNextPage();
                    }
                    break;

                case Key.PageDown:
                    pnlResult.SelectNextPage();
                    e.Handled = true;
                    break;

                case Key.U:
                    if (GlobalHotkey.Instance.CheckModifiers().CtrlPressed)
                    {
                        pnlResult.SelectPrevPage();
                    }
                    break;

                case Key.PageUp:
                    pnlResult.SelectPrevPage();
                    e.Handled = true;
                    break;

                case Key.Back:
                    if (BackKeyDownEvent != null)
                    {
                        BackKeyDownEvent(new WoxKeyDownEventArgs
                        {
                            Query = tbQuery.Text,
                            keyEventArgs = e
                        });
                    }
                    break;

                case Key.F1:
                    Process.Start("http://doc.getwox.com");
                    break;

                case Key.Enter:
                    Result activeResult = GetActiveResult();
                    if (GlobalHotkey.Instance.CheckModifiers().ShiftPressed)
                    {
                        ShowContextMenu(activeResult);
                    }
                    else
                    {
                        SelectResult(activeResult);
                    }
                    e.Handled = true;
                    break;

                case Key.D1:
                    SelectItem(1);
                    break;

                case Key.D2:
                    SelectItem(2);
                    break;

                case Key.D3:
                    SelectItem(3);
                    break;

                case Key.D4:
                    SelectItem(4);
                    break;

                case Key.D5:
                    SelectItem(5);
                    break;
                case Key.D6:
                    SelectItem(6);
                    break;

            }
        }

        private void DisplayPrevQuery()
        {
            var prev = QueryHistoryStorage.Instance.Previous();
            DisplayQueryHistory(prev);
        }

        private void DisplayNextQuery()
        {
            var nextQuery = QueryHistoryStorage.Instance.Next();
            DisplayQueryHistory(nextQuery);
        }

        private void DisplayQueryHistory(HistoryItem history)
        {
            if (history != null)
            {
                var historyMetadata = QueryHistoryStorage.MetaData;
                ChangeQueryText(history.Query, true);
                var executeQueryHistoryTitle = GetTranslation("executeQuery");
                var lastExecuteTime = GetTranslation("lastExecuteTime");
                pnlResult.RemoveResultsExcept(historyMetadata);
                UpdateResultViewInternal(new List<Result>
                {
                    new Result
                    {
                        Title = string.Format(executeQueryHistoryTitle,history.Query),
                        SubTitle = string.Format(lastExecuteTime,history.ExecutedDateTime),
                        IcoPath = "Images\\history.png",
                        PluginDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),
                        Action = _ =>{
                            ChangeQuery(history.Query,true);
                            return false;
                        }
                    }
                }, historyMetadata);
            }
        }

        private void SelectItem(int index)
        {
            int zeroBasedIndex = index - 1;
            SpecialKeyState keyState = GlobalHotkey.Instance.CheckModifiers();
            if (keyState.AltPressed || keyState.CtrlPressed)
            {
                List<Result> visibleResults = pnlResult.GetVisibleResults();
                if (zeroBasedIndex < visibleResults.Count)
                {
                    SelectResult(visibleResults[zeroBasedIndex]);
                }
            }
        }

        private bool IsInContextMenuMode
        {
            get { return pnlContextMenu.Visibility == Visibility.Visible; }
        }

        private Result GetActiveResult()
        {
            if (IsInContextMenuMode)
            {
                return pnlContextMenu.GetActiveResult();
            }
            else
            {
                return pnlResult.GetActiveResult();
            }
        }

        private void SelectPrevItem()
        {
            if (IsInContextMenuMode)
            {
                pnlContextMenu.SelectPrev();
            }
            else
            {
                pnlResult.SelectPrev();
            }
            toolTip.IsOpen = false;
        }

        private void SelectNextItem()
        {
            if (IsInContextMenuMode)
            {
                pnlContextMenu.SelectNext();
            }
            else
            {
                pnlResult.SelectNext();
            }
            toolTip.IsOpen = false;
        }

        private void SelectResult(Result result)
        {
            if (result != null)
            {
                if (result.Action != null)
                {
                    bool hideWindow = result.Action(new ActionContext
                    {
                        SpecialKeyState = GlobalHotkey.Instance.CheckModifiers()
                    });
                    if (hideWindow)
                    {
                        HideWox();
                    }
                    UserSelectedRecordStorage.Instance.Add(result);
                    QueryHistoryStorage.Instance.Add(tbQuery.Text);
                }
            }
        }

        private void UpdateResultView(List<Result> list, PluginMetadata metadata, Query originQuery)
        {
            _queryHasReturn = true;
            progressBar.Dispatcher.Invoke(StopProgress);

            list.ForEach(o =>
            {
                o.Score += UserSelectedRecordStorage.Instance.GetSelectedCount(o) * 5;
            });
            if (originQuery.RawQuery == _lastQuery.RawQuery)
            {
                UpdateResultViewInternal(list, metadata);
            }
        }

        private void UpdateResultViewInternal(List<Result> list, PluginMetadata metadata)
        {
            Dispatcher.Invoke(() =>
            {
                Stopwatch.Normal($"UI update cost for {metadata.Name}",
                    () => { pnlResult.AddResults(list, metadata.ID); });
            });
        }

        private Result GetTopMostContextMenu(Result result)
        {
            if (TopMostRecordStorage.Instance.IsTopMost(result))
            {
                return new Result(GetTranslation("cancelTopMostInThisQuery"), "Images\\down.png")
                {
                    PluginDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),
                    Action = _ =>
                    {
                        TopMostRecordStorage.Instance.Remove(result);
                        ShowMsg("Succeed", "", "");
                        return false;
                    }
                };
            }
            else
            {
                return new Result(GetTranslation("setAsTopMostInThisQuery"), "Images\\up.png")
                {
                    PluginDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),
                    Action = _ =>
                    {
                        TopMostRecordStorage.Instance.AddOrUpdate(result);
                        ShowMsg("Succeed", "", "");
                        return false;
                    }
                };
            }
        }

        private void ShowContextMenu(Result result)
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

            textBeforeEnterContextMenuMode = tbQuery.Text;
            ChangeQueryText("");
            pnlContextMenu.Clear();
            pnlContextMenu.AddResults(results, result.PluginID);
            CurrentContextMenus = results;
            pnlContextMenu.Visibility = Visibility.Visible;
            pnlResult.Visibility = Visibility.Collapsed;
        }

        private void MainWindow_OnDrop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                // Note that you can have more than one file.
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files[0].ToLower().EndsWith(".wox"))
                {
                    PluginManager.InstallPlugin(files[0]);
                }
                else
                {
                    MessageBox.Show(InternationalizationManager.Instance.GetTranslation("invalidWoxPluginFileFormat"));
                }
            }
        }

        private void TbQuery_OnPreviewDragOver(object sender, DragEventArgs e)
        {
            e.Handled = true;
        }
    }
}