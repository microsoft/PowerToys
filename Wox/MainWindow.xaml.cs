using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using WindowsInput;
using WindowsInput.Native;
using NHotkey;
using NHotkey.Wpf;
using Wox.Commands;
using Wox.Helper;
using Wox.Infrastructure;
using Wox.Infrastructure.Storage;
using Wox.Infrastructure.Storage.UserSettings;
using Wox.Plugin;
using Wox.PluginLoader;
using Application = System.Windows.Application;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using ContextMenu = System.Windows.Forms.ContextMenu;
using DragEventArgs = System.Windows.DragEventArgs;
using FontFamily = System.Windows.Media.FontFamily;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using MenuItem = System.Windows.Forms.MenuItem;
using MessageBox = System.Windows.MessageBox;
using MouseButton = System.Windows.Input.MouseButton;
using Path = System.IO.Path;
using Rectangle = System.Drawing.Rectangle;
using TextBox = System.Windows.Controls.TextBox;
using ToolTip = System.Windows.Controls.ToolTip;

namespace Wox
{
    public partial class MainWindow : IPublicAPI
    {

        #region Properties

        private static readonly object locker = new object();
        public static bool initialized = false;

        private static readonly List<Result> waitShowResultList = new List<Result>();
        private readonly GlobalHotkey globalHotkey = new GlobalHotkey();
        private readonly KeyboardSimulator keyboardSimulator = new KeyboardSimulator(new InputSimulator());
        private readonly Storyboard progressBarStoryboard = new Storyboard();
        private bool WinRStroked;
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
            Dispatcher.Invoke(new Action(() => PluginInstaller.Install(path)));
        }

        public void ReloadPlugins()
        {
            Dispatcher.Invoke(new Action(Plugins.Init));
        }

        public List<PluginPair> GetAllPlugins()
        {
            return Plugins.AllPlugins;
        }

        public event WoxKeyDownEventHandler BackKeyDownEvent;

        public void PushResults(Query query, PluginMetadata plugin, List<Result> results)
        {
            results.ForEach(o =>
            {
                o.PluginDirectory = plugin.PluginDirectory;
                o.OriginQuery = query;
            });
            OnUpdateResultView(results);
        }

        #endregion


        public MainWindow()
        {
            InitializeComponent();

            if (UserSettingStorage.Instance.OpacityMode == OpacityMode.LayeredWindow)
                this.AllowsTransparency = true;

            System.Net.WebRequest.RegisterPrefix("data", new DataWebRequestFactory());

            progressBar.ToolTip = toolTip;
            InitialTray();
            resultCtrl.OnMouseClickItem += AcceptSelect;

            ThreadPool.SetMaxThreads(30, 10);
            try
            {
                SetTheme(UserSettingStorage.Instance.Theme);
            }
            catch (Exception)
            {
                SetTheme(UserSettingStorage.Instance.Theme = "Dark");
            }

            SetHotkey(UserSettingStorage.Instance.Hotkey, OnHotkey);
            SetCustomPluginHotkey();

            globalHotkey.hookedKeyboardCallback += KListener_hookedKeyboardCallback;

            this.Closing += MainWindow_Closing;
        }

        void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
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

            Plugins.Init();

            InitProgressbarAnimation();

            //only works for win7+
            if (UserSettingStorage.Instance.OpacityMode == OpacityMode.DWM)
                DwmDropShadow.DropShadowToWindow(this);

            this.Background = Brushes.Transparent;
            HwndSource.FromHwnd(new WindowInteropHelper(this).Handle).CompositionTarget.BackgroundColor = Color.FromArgb(0, 0, 0, 0);

            WindowIntelopHelper.DisableControlBox(this);
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
                MessageBox.Show("Register hotkey: " + hotkeyStr + " failed.");
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
            if (!IsVisible)
            {
                ShowWox();
            }
            else
            {
                HideWox();
            }
            e.Handled = true;
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
            resultCtrl.Dirty = true;
            Dispatcher.DelayInvoke("UpdateSearch",
                o =>
                {
                    Dispatcher.DelayInvoke("ClearResults", i =>
                    {
                        // first try to use clear method inside resultCtrl, which is more closer to the add new results
                        // and this will not bring splash issues.After waiting 30ms, if there still no results added, we
                        // must clear the result. otherwise, it will be confused why the query changed, but the results
                        // didn't.
                        if (resultCtrl.Dirty) resultCtrl.Clear();
                    }, TimeSpan.FromMilliseconds(100), null);
                    queryHasReturn = false;
                    var q = new Query(lastQuery);
                    CommandFactory.DispatchCommand(q);
                    if (Plugins.HitThirdpartyKeyword(q))
                    {
                        Dispatcher.DelayInvoke("ShowProgressbar", originQuery =>
                        {
                            if (!queryHasReturn && originQuery == lastQuery)
                            {
                                StartProgress();
                            }
                        }, TimeSpan.FromSeconds(0), lastQuery);
                    }
                }, TimeSpan.FromMilliseconds(ShouldNotDelayQuery ? 0 : 150));
        }

        private bool ShouldNotDelayQuery
        {
            get
            {
                return IsCMDMode || IsWebSearchMode;
            }
        }

        private bool IsCMDMode
        {
            get
            {
                return tbQuery.Text.StartsWith(">");
            }
        }

        private bool IsWebSearchMode
        {
            get
            {
                Query q = new Query(tbQuery.Text);
                return !UserSettingStorage.Instance.EnableWebSearchSuggestion &&
                        UserSettingStorage.Instance.WebSearches.Exists(o => o.ActionWord == q.ActionName && o.Enabled);
            }
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

        public void ParseArgs(string[] args)
        {
            if (args != null && args.Length > 0)
            {
                switch (args[0].ToLower())
                {
                    case "reloadplugin":
                        Plugins.Init();
                        break;

                    case "query":
                        if (args.Length > 1)
                        {
                            string query = args[1];
                            tbQuery.Text = query;
                            tbQuery.SelectAll();
                        }
                        break;

                    case "hidestart":
                        HideApp();
                        break;

                    case "installplugin":
                        var path = args[1];
                        if (!File.Exists(path))
                        {
                            MessageBox.Show("Plugin " + path + " didn't exist");
                            return;
                        }
                        PluginInstaller.Install(path);
                        break;
                }
            }
        }

        private void MainWindow_OnDeactivated(object sender, EventArgs e)
        {
            if (UserSettingStorage.Instance.HideWhenDeactive)
            {
                HideWox();
            }
        }

        private bool KListener_hookedKeyboardCallback(KeyEvent keyevent, int vkcode, SpecialKeyState state)
        {
            if (UserSettingStorage.Instance.ReplaceWinR)
            {
                //todo:need refactoring. move those codes to CMD file or expose events
                if (keyevent == KeyEvent.WM_KEYDOWN && vkcode == (int)Keys.R && state.WinPressed)
                {
                    WinRStroked = true;
                    Dispatcher.BeginInvoke(new Action(OnWinRPressed));
                    return false;
                }
                if (keyevent == KeyEvent.WM_KEYUP && WinRStroked && vkcode == (int)Keys.LWin)
                {
                    WinRStroked = false;
                    keyboardSimulator.ModifiedKeyStroke(VirtualKeyCode.LWIN, VirtualKeyCode.CONTROL);
                    return false;
                }
            }
            return true;
        }

        private void OnWinRPressed()
        {
            ShowWox(false);
            if (!tbQuery.Text.StartsWith(">"))
            {
                resultCtrl.Clear();
                ChangeQuery(">");
            }
            tbQuery.CaretIndex = tbQuery.Text.Length;
            tbQuery.SelectionStart = 1;
            tbQuery.SelectionLength = tbQuery.Text.Length - 1;
        }

        private void updateCmdMode()
        {
            var currentSelectedItem = resultCtrl.GetActiveResult();
            if (currentSelectedItem != null)
            {
                ignoreTextChange = true;
                tbQuery.Text = ">" + currentSelectedItem.Title;
                tbQuery.CaretIndex = tbQuery.Text.Length;
            }
        }

        private void TbQuery_OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            //when alt is pressed, the real key should be e.SystemKey
            Key key = (e.Key == Key.System ? e.SystemKey : e.Key);
            switch (key)
            {
                case Key.Escape:
                    HideWox();
                    e.Handled = true;
                    break;

                case Key.Tab:
                    if (globalHotkey.CheckModifiers().ShiftPressed)
                    {
                        SelectPrevItem();
                    }
                    else
                    {
                        SelectNextItem();
                    }
                    e.Handled = true;
                    break;

                case Key.Down:
                    SelectNextItem();
                    e.Handled = true;
                    break;

                case Key.Up:
                    SelectPrevItem();
                    e.Handled = true;
                    break;

                case Key.PageDown:
                    resultCtrl.SelectNextPage();
                    if (IsCMDMode) updateCmdMode();
                    toolTip.IsOpen = false;
                    e.Handled = true;
                    break;

                case Key.PageUp:
                    resultCtrl.SelectPrevPage();
                    if (IsCMDMode) updateCmdMode();
                    toolTip.IsOpen = false;
                    e.Handled = true;
                    break;

                case Key.Back:
                    if (BackKeyDownEvent != null)
                    {
                        BackKeyDownEvent(tbQuery, new WoxKeyDownEventArgs()
                        {
                            Query = tbQuery.Text,
                            keyEventArgs = e
                        });
                    }
                    break;

                case Key.F1:
                    Process.Start("https://github.com/qianlifeng/Wox/wiki/Wox-Function-Guide");
                    break;

                case Key.Enter:
                    AcceptSelect(resultCtrl.GetActiveResult());
                    e.Handled = true;
                    break;
            }
        }

        private void SelectPrevItem()
        {
            resultCtrl.SelectPrev();
            if (IsCMDMode) updateCmdMode();
            toolTip.IsOpen = false;
        }

        private void SelectNextItem()
        {
            resultCtrl.SelectNext();
            if (IsCMDMode) updateCmdMode();
            toolTip.IsOpen = false;
        }

        private void AcceptSelect(Result result)
        {
            if (!resultCtrl.Dirty && result != null)
            {
                if (result.Action != null)
                {
                    bool hideWindow = result.Action(new ActionContext()
                    {
                        SpecialKeyState = globalHotkey.CheckModifiers()
                    });
                    if (hideWindow)
                    {
                        HideWox();
                    }
                    UserSelectedRecordStorage.Instance.Add(result);
                }
            }
        }

        public void OnUpdateResultView(List<Result> list)
        {
            queryHasReturn = true;
            progressBar.Dispatcher.Invoke(new Action(StopProgress));
            if (list == null || list.Count == 0) return;

            if (list.Count > 0)
            {
                //todo:this should be opened to users, it's their choice to use it or not in their workflows
                list.ForEach(
                    o =>
                    {
                        if (o.AutoAjustScore) o.Score += UserSelectedRecordStorage.Instance.GetSelectedCount(o);
                    });
                List<Result> l = list.Where(o => o.OriginQuery != null && o.OriginQuery.RawQuery == lastQuery).ToList();
                Dispatcher.Invoke(new Action(() => resultCtrl.AddResults(l)));
            }
        }

        public void SetTheme(string themeName)
        {
            var dict = new ResourceDictionary
            {
                Source = new Uri(Path.Combine(Path.GetDirectoryName(System.Windows.Forms.Application.ExecutablePath), "Themes\\" + themeName + ".xaml"), UriKind.Absolute)
            };


            Style queryBoxStyle = dict["QueryBoxStyle"] as Style;
            if (queryBoxStyle != null)
            {
                queryBoxStyle.Setters.Add(new Setter(TextBox.FontFamilyProperty, new FontFamily(UserSettingStorage.Instance.QueryBoxFont)));
                queryBoxStyle.Setters.Add(new Setter(TextBox.FontStyleProperty, FontHelper.GetFontStyleFromInvariantStringOrNormal(UserSettingStorage.Instance.QueryBoxFontStyle)));
                queryBoxStyle.Setters.Add(new Setter(TextBox.FontWeightProperty, FontHelper.GetFontWeightFromInvariantStringOrNormal(UserSettingStorage.Instance.QueryBoxFontWeight)));
                queryBoxStyle.Setters.Add(new Setter(TextBox.FontStretchProperty, FontHelper.GetFontStretchFromInvariantStringOrNormal(UserSettingStorage.Instance.QueryBoxFontStretch)));
            }

            Style resultItemStyle = dict["ItemTitleStyle"] as Style;
            Style resultSubItemStyle = dict["ItemSubTitleStyle"] as Style;
            Style resultItemSelectedStyle = dict["ItemTitleSelectedStyle"] as Style;
            Style resultSubItemSelectedStyle = dict["ItemSubTitleSelectedStyle"] as Style;
            if (resultItemStyle != null && resultSubItemStyle != null && resultSubItemSelectedStyle != null && resultItemSelectedStyle != null)
            {
                Setter fontFamily = new Setter(TextBlock.FontFamilyProperty, new FontFamily(UserSettingStorage.Instance.ResultItemFont));
                Setter fontStyle = new Setter(TextBlock.FontStyleProperty, FontHelper.GetFontStyleFromInvariantStringOrNormal(UserSettingStorage.Instance.ResultItemFontStyle));
                Setter fontWeight = new Setter(TextBlock.FontWeightProperty, FontHelper.GetFontWeightFromInvariantStringOrNormal(UserSettingStorage.Instance.ResultItemFontWeight));
                Setter fontStretch = new Setter(TextBlock.FontStretchProperty, FontHelper.GetFontStretchFromInvariantStringOrNormal(UserSettingStorage.Instance.ResultItemFontStretch));

                Setter[] setters = new Setter[] { fontFamily, fontStyle, fontWeight, fontStretch };
                Array.ForEach(new Style[] { resultItemStyle, resultSubItemStyle, resultItemSelectedStyle, resultSubItemSelectedStyle }, o => Array.ForEach(setters, p => o.Setters.Add(p)));
            }

            Application.Current.Resources.MergedDictionaries.Clear();
            Application.Current.Resources.MergedDictionaries.Add(dict);

            this.Opacity = this.AllowsTransparency ? UserSettingStorage.Instance.Opacity : 1;
        }

        public bool ShellRun(string cmd)
        {
            try
            {
                if (string.IsNullOrEmpty(cmd))
                    throw new ArgumentNullException();

                Wox.Infrastructure.WindowsShellRun.Start(cmd);
                return true;
            }
            catch (Exception ex)
            {
                ShowMsg("Could not start " + cmd, ex.Message, null);
            }
            return false;
        }

        private void MainWindow_OnDrop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop))
            {
                // Note that you can have more than one file.
                string[] files = (string[])e.Data.GetData(System.Windows.DataFormats.FileDrop);
                if (files[0].ToLower().EndsWith(".wox"))
                {
                    PluginInstaller.Install(files[0]);
                }
                else
                {
                    MessageBox.Show("incorrect wox plugin file.");
                }
            }
        }

        private void TbQuery_OnPreviewDragOver(object sender, DragEventArgs e)
        {
            e.Handled = true;
        }
    }
}