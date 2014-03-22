using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using WindowsInput;
using WindowsInput.Native;
using NHotkey;
using NHotkey.Wpf;
using Wox.Commands;
using Wox.Helper;
using Wox.Infrastructure;
using Wox.Infrastructure.UserSettings;
using Wox.Plugin;
using Wox.PluginLoader;
using Application = System.Windows.Application;
using ContextMenu = System.Windows.Forms.ContextMenu;
using Control = System.Windows.Controls.Control;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using MenuItem = System.Windows.Forms.MenuItem;
using MessageBox = System.Windows.MessageBox;
using MouseButton = System.Windows.Input.MouseButton;
using TextBox = System.Windows.Controls.TextBox;
using ToolTip = System.Windows.Controls.ToolTip;

namespace Wox
{
    public partial class MainWindow
    {
        private static readonly object locker = new object();
        public static bool initialized = false;

        private static readonly List<Result> waitShowResultList = new List<Result>();
        private readonly GlobalHotkey globalHotkey = new GlobalHotkey();
        private readonly KeyboardSimulator keyboardSimulator = new KeyboardSimulator(new InputSimulator());
        private readonly Storyboard progressBarStoryboard = new Storyboard();
        private bool WinRStroked;
        private NotifyIcon notifyIcon;
        private bool queryHasReturn;
        private ToolTip toolTip = new ToolTip();
        public MainWindow()
        {
            InitializeComponent();
            initialized = true;

            System.Net.WebRequest.RegisterPrefix("data", new DataWebRequestFactory());
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

            progressBar.ToolTip = toolTip;
            InitialTray();
            resultCtrl.OnMouseClickItem += AcceptSelect;

            ThreadPool.SetMaxThreads(30, 10);
            try
            {
                SetTheme(CommonStorage.Instance.UserSetting.Theme);
            }
            catch (Exception)
            {
                SetTheme(CommonStorage.Instance.UserSetting.Theme = "Dark");
            }

            SetHotkey(CommonStorage.Instance.UserSetting.Hotkey, OnHotkey);
            SetCustomPluginHotkey();

            globalHotkey.hookedKeyboardCallback += KListener_hookedKeyboardCallback;

            this.Closing += new System.ComponentModel.CancelEventHandler(MainWindow_Closing);
        }

        void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            this.HideWox();
            e.Cancel = true;
        }

        private void MainWindow_OnLoaded(object sender, RoutedEventArgs e)
        {
            Left = (SystemParameters.PrimaryScreenWidth - ActualWidth) / 2;
            Top = (SystemParameters.PrimaryScreenHeight - ActualHeight) / 3;
            Plugins.Init();

            InitProgressbarAnimation();
            //only works for win7+
            //DwmDropShadow.DropShadowToWindow(this);

            WindowIntelopHelper.DisableControlBox(this);
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            if (!System.Diagnostics.Debugger.IsAttached)
            {
                string error = "Wox has an error that can't be handled. " + e.ExceptionObject;
                Log.Error(error);
                if (e.IsTerminating)
                {
                    notifyIcon.Visible = false;
                    MessageBox.Show(error);
                }
            }
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
            if (CommonStorage.Instance.UserSetting.CustomPluginHotkeys == null) return;
            foreach (CustomPluginHotkey hotkey in CommonStorage.Instance.UserSetting.CustomPluginHotkeys)
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
            var exit = new MenuItem("Exit");
            exit.Click += (o, e) => CloseApp();
            MenuItem[] childen = { open, exit };
            notifyIcon.ContextMenu = new ContextMenu(childen);
        }

        private bool isCMDMode = false;
        private bool ignoreTextChange = false;
        private void TextBoxBase_OnTextChanged(object sender, TextChangedEventArgs e)
        {
            if (ignoreTextChange) { ignoreTextChange = false; return; }

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
                    var q = new Query(tbQuery.Text);
                    CommandFactory.DispatchCommand(q);
                    queryHasReturn = false;
                    if (Plugins.HitThirdpartyKeyword(q))
                    {
                        Dispatcher.DelayInvoke("ShowProgressbar", originQuery =>
                        {
                            if (!queryHasReturn && originQuery == tbQuery.Text)
                            {
                                StartProgress();
                            }
                        }, TimeSpan.FromSeconds(0), tbQuery.Text);
                    }
                }, TimeSpan.FromMilliseconds((isCMDMode = tbQuery.Text.StartsWith(">")) ? 0 : 150));
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



        private bool KListener_hookedKeyboardCallback(KeyEvent keyevent, int vkcode, SpecialKeyState state)
        {
            if (CommonStorage.Instance.UserSetting.ReplaceWinR)
            {
                //todo:need refatoring. move those codes to CMD file or expose events
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
            var selected = resultCtrl.AcceptSelect();
            if (selected != null)
            {
                ignoreTextChange = true;
                tbQuery.Text = ">" + selected.Title;
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

                case Key.Down:
                    resultCtrl.SelectNext();
                    if (isCMDMode) updateCmdMode();
                    toolTip.IsOpen = false;
                    e.Handled = true;
                    break;

                case Key.Up:
                    resultCtrl.SelectPrev();
                    if (isCMDMode) updateCmdMode();
                    toolTip.IsOpen = false;
                    e.Handled = true;
                    break;

                case Key.PageDown:
                    resultCtrl.SelectNextPage();
                    if (isCMDMode) updateCmdMode();
                    toolTip.IsOpen = false;
                    e.Handled = true;
                    break;

                case Key.PageUp:
                    resultCtrl.SelectPrevPage();
                    if (isCMDMode) updateCmdMode();
                    toolTip.IsOpen = false;
                    e.Handled = true;
                    break;

                case Key.Enter:
                    AcceptSelect(resultCtrl.AcceptSelect());
                    e.Handled = true;
                    break;
            }
        }

        private void AcceptSelect(Result result)
        {
            if (result != null)
            {
                if (result.Action != null)
                {
                    bool hideWindow = result.Action(new ActionContext()
                    {
                        SpecialKeyState = new GlobalHotkey().CheckModifiers()
                    });
                    if (hideWindow)
                    {
                        HideWox();
                    }
                    CommonStorage.Instance.UserSelectedRecords.Add(result);
                }
            }
        }

        public void OnUpdateResultView(List<Result> list)
        {
            if (list == null) return;

            queryHasReturn = true;
            progressBar.Dispatcher.Invoke(new Action(StopProgress));
            if (list.Count > 0)
            {
                //todo:this should be opened to users, it's their choise to use it or not in thier workflows
                list.ForEach(
                    o =>
                    {
                        if (o.AutoAjustScore) o.Score += CommonStorage.Instance.UserSelectedRecords.GetSelectedCount(o);
                    });
                lock (locker)
                {
                    waitShowResultList.AddRange(list);
                }
                Dispatcher.DelayInvoke("ShowResult", k => resultCtrl.Dispatcher.Invoke(new Action(() =>
                {
                    List<Result> l = waitShowResultList.Where(o => o.OriginQuery != null && o.OriginQuery.RawQuery == tbQuery.Text).ToList();
                    waitShowResultList.Clear();
                    resultCtrl.AddResults(l);
                })), TimeSpan.FromMilliseconds(isCMDMode ? 0 : 50));
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
                queryBoxStyle.Setters.Add(new Setter(TextBox.FontFamilyProperty, new FontFamily(CommonStorage.Instance.UserSetting.QueryBoxFont)));
            }
            Style resultItemStyle = dict["ItemTitleStyle"] as Style;
            Style resultSubItemStyle = dict["ItemSubTitleStyle"] as Style;
            Style resultItemSelectedStyle = dict["ItemTitleSelectedStyle"] as Style;
            Style resultSubItemSelectedStyle = dict["ItemSubTitleSelectedStyle"] as Style;
            if (resultItemStyle != null && resultSubItemStyle != null
                && resultSubItemSelectedStyle != null && resultItemSelectedStyle != null)
            {
                Setter fontFamily = new Setter(TextBlock.FontFamilyProperty, new FontFamily(CommonStorage.Instance.UserSetting.ResultItemFont));

                resultItemStyle.Setters.Add(fontFamily);
                resultSubItemStyle.Setters.Add(fontFamily);
                resultItemSelectedStyle.Setters.Add(fontFamily);
                resultSubItemSelectedStyle.Setters.Add(fontFamily);
            }

            Application.Current.Resources.MergedDictionaries.Clear();
            Application.Current.Resources.MergedDictionaries.Add(dict);
        }

        #region Public API

        public void ChangeQuery(string query, bool requery = false)
        {
            tbQuery.Text = query;
            tbQuery.CaretIndex = tbQuery.Text.Length;
            if (requery)
            {
                TextBoxBase_OnTextChanged(null, null);
            }
        }

        public void CloseApp()
        {
            notifyIcon.Visible = false;
            Close();
            Environment.Exit(0);
        }

        public void HideApp()
        {
            HideWox();
        }

        public void ShowApp()
        {
            ShowWox();
        }

        public void ShowMsg(string title, string subTitle, string iconPath)
        {
            var m = new Msg { Owner = GetWindow(this) };
            m.Show(title, subTitle, iconPath);
        }

        public void OpenSettingDialog()
        {
            new SettingWindow(this).Show();
        }

        public void ShowCurrentResultItemTooltip(string text)
        {
            toolTip.Content = text;
            toolTip.IsOpen = true;
        }

        public void StartLoadingBar()
        {
            Dispatcher.Invoke(new Action(StartProgress));
        }

        public void StopLoadingBar()
        {
            Dispatcher.Invoke(new Action(StopProgress));
        }

        #endregion

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
    }
}