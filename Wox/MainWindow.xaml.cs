using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Interop;
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
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using ContextMenu = System.Windows.Forms.ContextMenu;
using DataFormats = System.Windows.DataFormats;
using DragEventArgs = System.Windows.DragEventArgs;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using MenuItem = System.Windows.Forms.MenuItem;
using MessageBox = System.Windows.MessageBox;
using ToolTip = System.Windows.Controls.ToolTip;
using Wox.Infrastructure.Logger;
using IDataObject = System.Windows.IDataObject;
using System.IO;

namespace Wox
{
    public partial class MainWindow : IPublicAPI
    {

        #region Properties

        private readonly Storyboard progressBarStoryboard = new Storyboard();
        private NotifyIcon notifyIcon;
        private bool queryHasReturn;
        private string lastQuery;
        private ToolTip toolTip = new ToolTip();

        private bool ignoreTextChange = false;

        #endregion

        #region Public API

        public void ChangeQuery(string query, bool requery = false)
        {
            Dispatcher.Invoke(new Action(() =>
            {
                tbQuery.Text = query;
                tbQuery.CaretIndex = tbQuery.Text.Length;
                if (requery)
                {
                    TextBoxBase_OnTextChanged(null, null);
                }
            }));
        }

        public void CloseApp()
        {
            Dispatcher.Invoke(new Action(() =>
            {
                notifyIcon.Visible = false;
                Close();
                Environment.Exit(0);
            }));
        }

        public void HideApp()
        {
            Dispatcher.Invoke(new Action(HideWox));
        }

        public void ShowApp()
        {
            Dispatcher.Invoke(new Action(() => ShowWox()));
        }

        public void ShowMsg(string title, string subTitle, string iconPath)
        {
            Dispatcher.Invoke(new Action(() =>
            {
                var m = new Msg { Owner = GetWindow(this) };
                m.Show(title, subTitle, iconPath);
            }));
        }

        public void OpenSettingDialog()
        {
            Dispatcher.Invoke(new Action(() => WindowOpener.Open<SettingWindow>(this)));
        }

        public void StartLoadingBar()
        {
            Dispatcher.Invoke(new Action(StartProgress));
        }

        public void StopLoadingBar()
        {
            Dispatcher.Invoke(new Action(StopProgress));
        }

        public void InstallPlugin(string path)
        {
            Dispatcher.Invoke(new Action(() => PluginManager.InstallPlugin(path)));
        }

        public void ReloadPlugins()
        {
            Dispatcher.Invoke(new Action(() => PluginManager.Init(this)));
        }

        public string GetTranslation(string key)
        {
            return InternationalizationManager.Instance.GetTranslation(key);
        }

        public List<PluginPair> GetAllPlugins()
        {
            return PluginManager.AllPlugins;
        }

        public event WoxKeyDownEventHandler BackKeyDownEvent;
        public event WoxGlobalKeyboardEventHandler GlobalKeyboardEvent;
        public event AfterWoxQueryEventHandler AfterWoxQueryEvent;
        public event AfterWoxQueryEventHandler BeforeWoxQueryEvent;
        public event ResultItemDropEventHandler ResultItemDropEvent;

        public void PushResults(Query query, PluginMetadata plugin, List<Result> results)
        {
            results.ForEach(o =>
            {
                o.PluginDirectory = plugin.PluginDirectory;
                o.PluginID = plugin.ID;
                o.OriginQuery = query;
                if (o.ContextMenu != null)
                {
                    o.ContextMenu.ForEach(t =>
                    {
                        t.PluginDirectory = plugin.PluginDirectory;
                        t.PluginID = plugin.ID;
                    });
                }
            });
            UpdateResultView(results);
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
                pnlContextMenu.AddResults(results);
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
            InitialTray();
            pnlResult.LeftMouseClickEvent += SelectResult;
            pnlResult.ItemDropEvent += pnlResult_ItemDropEvent;
            pnlContextMenu.LeftMouseClickEvent += SelectResult;
            pnlResult.RightMouseClickEvent += pnlResult_RightMouseClickEvent;

            ThemeManager.Theme.ChangeTheme(UserSettingStorage.Instance.Theme);
            InternationalizationManager.Instance.ChangeLanguage(UserSettingStorage.Instance.Language);

            SetHotkey(UserSettingStorage.Instance.Hotkey, OnHotkey);
            SetCustomPluginHotkey();

            Closing += MainWindow_Closing;
            //since MainWIndow implement IPublicAPI, so we need to finish ctor MainWindow object before
            //PublicAPI invoke in plugin init methods. E.g FolderPlugin
            ThreadPool.QueueUserWorkItem(o =>
            {
                Thread.Sleep(50);
                PluginManager.Init(this);
            });
            ThreadPool.QueueUserWorkItem(o =>
            {
                Thread.Sleep(50);
                PreLoadImages();
            });
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

        private void PreLoadImages()
        {
            ImageLoader.ImageLoader.PreloadImages();
        }

        void pnlResult_RightMouseClickEvent(Result result)
        {
            ShowContextMenuFromResult(result);
        }

        void MainWindow_Closing(object sender, CancelEventArgs e)
        {
            UserSettingStorage.Instance.WindowLeft = Left;
            UserSettingStorage.Instance.WindowTop = Top;
            UserSettingStorage.Instance.Save();
            this.HideWox();
            e.Cancel = true;
        }

        private void MainWindow_OnLoaded(object sender, RoutedEventArgs e)
        {
            if (UserSettingStorage.Instance.WindowLeft == 0
                && UserSettingStorage.Instance.WindowTop == 0)
            {
                Left = UserSettingStorage.Instance.WindowLeft
                     = (SystemParameters.PrimaryScreenWidth - ActualWidth) / 2;
                Top = UserSettingStorage.Instance.WindowTop
                    = (SystemParameters.PrimaryScreenHeight - ActualHeight) / 5;
            }
            else
            {
                Left = UserSettingStorage.Instance.WindowLeft;
                Top = UserSettingStorage.Instance.WindowTop;
            }

            InitProgressbarAnimation();
            WindowIntelopHelper.DisableControlBox(this);
            CheckUpdate();
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
            Dispatcher.Invoke(new Action(() =>
            {
                new WoxUpdate().ShowDialog();
            }));
        }

        public void SetHotkey(string hotkeyStr, EventHandler<HotkeyEventArgs> action)
        {
            var hotkey = new HotkeyModel(hotkeyStr);
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

        private void SetCustomPluginHotkey()
        {
            if (UserSettingStorage.Instance.CustomPluginHotkeys == null) return;
            foreach (CustomPluginHotkey hotkey in UserSettingStorage.Instance.CustomPluginHotkeys)
            {
                CustomPluginHotkey hotkey1 = hotkey;
                SetHotkey(hotkey.Hotkey, delegate
                {
                    ShowApp();
                    ChangeQuery(hotkey1.ActionKeyword, true);
                });
            }
        }

        private void OnHotkey(object sender, HotkeyEventArgs e)
        {
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
            var open = new MenuItem("Open");
            open.Click += (o, e) => ShowWox();
            var setting = new MenuItem("Settings");
            setting.Click += (o, e) => OpenSettingDialog();
            var exit = new MenuItem("Exit");
            exit.Click += (o, e) => CloseApp();
            MenuItem[] childen = { open, setting, exit };
            notifyIcon.ContextMenu = new ContextMenu(childen);
        }

        private void TextBoxBase_OnTextChanged(object sender, TextChangedEventArgs e)
        {
            if (ignoreTextChange) { ignoreTextChange = false; return; }

            lastQuery = tbQuery.Text;
            toolTip.IsOpen = false;
            pnlResult.Dirty = true;
            int searchDelay = GetSearchDelay(lastQuery);

            Dispatcher.DelayInvoke("UpdateSearch",
                o =>
                {
                    Dispatcher.DelayInvoke("ClearResults", i =>
                    {
                        // first try to use clear method inside pnlResult, which is more closer to the add new results
                        // and this will not bring splash issues.After waiting 100ms, if there still no results added, we
                        // must clear the result. otherwise, it will be confused why the query changed, but the results
                        // didn't.
                        if (pnlResult.Dirty) pnlResult.Clear();
                    }, TimeSpan.FromMilliseconds(100), null);
                    queryHasReturn = false;
                    Query query = new Query(lastQuery);
                    query.IsIntantQuery = searchDelay == 0;
                    FireBeforeWoxQueryEvent(query);
                    Query(query);
                    Dispatcher.DelayInvoke("ShowProgressbar", originQuery =>
                    {
                        if (!queryHasReturn && originQuery == tbQuery.Text && !string.IsNullOrEmpty(lastQuery))
                        {
                            StartProgress();
                        }
                    }, TimeSpan.FromMilliseconds(150), tbQuery.Text);
                    FireAfterWoxQueryEvent(query);
                }, TimeSpan.FromMilliseconds(searchDelay));
        }

        private int GetSearchDelay(string query)
        {
            if (!string.IsNullOrEmpty(query) && PluginManager.IsInstantQuery(query))
            {
                DebugHelper.WriteLine("execute query without delay");
                return 0;
            }

            DebugHelper.WriteLine("execute query with 200ms delay");
            return 200;
        }

        private void FireAfterWoxQueryEvent(Query q)
        {
            if (AfterWoxQueryEvent != null)
            {
                //We shouldn't let those events slow down real query
                //so I put it in the new thread
                ThreadPool.QueueUserWorkItem(o =>
                {
                    AfterWoxQueryEvent(new WoxQueryEventArgs()
                    {
                        Query = q
                    });
                });
            }
        }

        private void FireBeforeWoxQueryEvent(Query q)
        {
            if (BeforeWoxQueryEvent != null)
            {
                //We shouldn't let those events slow down real query
                //so I put it in the new thread
                ThreadPool.QueueUserWorkItem(o =>
                {
                    BeforeWoxQueryEvent(new WoxQueryEventArgs()
                    {
                        Query = q
                    });
                });
            }
        }

        private void Query(Query q)
        {
            PluginManager.Query(q);
            StopProgress();
            BackToResultMode();
        }

        private void BackToResultMode()
        {
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
            Hide();
        }

        private void ShowWox(bool selectAll = true)
        {
            UserSettingStorage.Instance.IncreaseActivateTimes();
            if (!double.IsNaN(Left) && !double.IsNaN(Top))
            {
                var origScreen = Screen.FromRectangle(new Rectangle((int)Left, (int)Top, (int)ActualWidth, (int)ActualHeight));
                var screen = Screen.FromPoint(System.Windows.Forms.Cursor.Position);
                var coordX = (Left - origScreen.WorkingArea.Left) / (origScreen.WorkingArea.Width - ActualWidth);
                var coordY = (Top - origScreen.WorkingArea.Top) / (origScreen.WorkingArea.Height - ActualHeight);
                Left = (screen.WorkingArea.Width - ActualWidth) * coordX + screen.WorkingArea.Left;
                Top = (screen.WorkingArea.Height - ActualHeight) * coordY + screen.WorkingArea.Top;
            }

            Show();
            Activate();
            Focus();
            tbQuery.Focus();
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
                    if (GlobalHotkey.Instance.CheckModifiers().AltPressed)
                    {
                        SelectNextItem();
                    }
                    break;

                case Key.P:
                case Key.K:
                    if (GlobalHotkey.Instance.CheckModifiers().AltPressed)
                    {
                        SelectPrevItem();
                    }
                    break;

                case Key.O:
                    if (GlobalHotkey.Instance.CheckModifiers().AltPressed)
                    {
                        if (IsInContextMenuMode)
                        {
                            BackToResultMode();
                        }
                        else
                        {
                            ShowContextMenuFromResult(GetActiveResult());
                        }
                    }
                    break;

                case Key.Down:
                    SelectNextItem();
                    e.Handled = true;
                    break;

                case Key.Up:
                    SelectPrevItem();
                    e.Handled = true;
                    break;

                case Key.D:
                    if (GlobalHotkey.Instance.CheckModifiers().AltPressed)
                    {
                        pnlResult.SelectNextPage();
                    }
                    break;

                case Key.PageDown:
                    pnlResult.SelectNextPage();
                    e.Handled = true;
                    break;

                case Key.U:
                    if (GlobalHotkey.Instance.CheckModifiers().AltPressed)
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
                        BackKeyDownEvent(new WoxKeyDownEventArgs()
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
                        ShowContextMenuFromResult(activeResult);
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
                    bool hideWindow = result.Action(new ActionContext()
                    {
                        SpecialKeyState = GlobalHotkey.Instance.CheckModifiers()
                    });
                    if (hideWindow)
                    {
                        HideWox();
                    }
                    UserSelectedRecordStorage.Instance.Add(result);
                }
            }
        }

        private void UpdateResultView(List<Result> list)
        {
            queryHasReturn = true;
            progressBar.Dispatcher.Invoke(new Action(StopProgress));
            if (list == null || list.Count == 0) return;

            if (list.Count > 0)
            {
                list.ForEach(o =>
                {
                    o.Score += UserSelectedRecordStorage.Instance.GetSelectedCount(o) * 5;
                    if (o.ContextMenu == null)
                    {
                        o.ContextMenu = new List<Result>();
                    }
                    HanleTopMost(o);
                });
                List<Result> l = list.Where(o => o.OriginQuery != null && o.OriginQuery.RawQuery == lastQuery).ToList();
                Dispatcher.Invoke(new Action(() =>
                {
                    pnlResult.AddResults(l);
                }));
            }
        }

        private void HanleTopMost(Result result)
        {
            if (TopMostRecordStorage.Instance.IsTopMost(result))
            {
                result.ContextMenu.Add(new Result("Remove top most in this query", "Images\\topmost.png")
                {
                    PluginDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),
                    Action = _ =>
                    {
                        TopMostRecordStorage.Instance.Remove(result);
                        ShowMsg("Succeed", "", "");
                        return false;
                    }
                });
            }
            else
            {
                result.ContextMenu.Add(new Result("Set as top most in this query", "Images\\topmost.png")
                {
                    PluginDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),
                    Action = _ =>
                    {
                        TopMostRecordStorage.Instance.Add(result);
                        ShowMsg("Succeed", "", "");
                        return false;
                    }
                });
            }
        }

        private void ShowContextMenuFromResult(Result result)
        {
            if (result.ContextMenu != null && result.ContextMenu.Count > 0)
            {
                pnlContextMenu.Clear();
                pnlContextMenu.AddResults(result.ContextMenu);
                pnlContextMenu.Visibility = Visibility.Visible;
                pnlResult.Visibility = Visibility.Collapsed;
            }
        }

        public bool ShellRun(string cmd, bool runAsAdministrator = false)
        {
            try
            {
                if (string.IsNullOrEmpty(cmd))
                    throw new ArgumentNullException();

                WindowsShellRun.Start(cmd, runAsAdministrator);
                return true;
            }
            catch (Exception ex)
            {
                string errorMsg = string.Format(InternationalizationManager.Instance.GetTranslation("couldnotStartCmd"), cmd);
                ShowMsg(errorMsg, ex.Message, null);
            }
            return false;
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