using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media.Animation;
using Wox.Commands;
using Wox.Helper;
using Wox.Infrastructure;
using Wox.Plugin;
using Wox.PluginLoader;
using Application = System.Windows.Application;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using MessageBox = System.Windows.MessageBox;
using UserControl = System.Windows.Controls.UserControl;

namespace Wox
{
    public partial class MainWindow
    {
        private KeyboardHook hook = new KeyboardHook();
        private NotifyIcon notifyIcon;
        Storyboard progressBarStoryboard = new Storyboard();
        private bool queryHasReturn = false;

        private KeyboardListener keyboardListener = new KeyboardListener();
        private bool WinRStroked = false;
        private WindowsInput.KeyboardSimulator keyboardSimulator = new WindowsInput.KeyboardSimulator(new WindowsInput.InputSimulator());

        public MainWindow()
        {
            InitializeComponent();

            InitialTray();
            hook.KeyPressed += OnHotKey;
            hook.RegisterHotKey(XModifierKeys.Alt, Keys.Space);
            ThreadPool.SetMaxThreads(30, 10);
            InitProgressbarAnimation();
            try
            {
                SetTheme(CommonStorage.Instance.UserSetting.Theme);
            }
            catch (IOException)
            {
                SetTheme(CommonStorage.Instance.UserSetting.Theme = "Default");
            }

            this.Closing += MainWindow_Closing;
        }

        void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            e.Cancel = true;
        }

        private void WakeupApp()
        {
            //After hide wox in the background for a long time. It will become very slow in the next show.
            //This is caused by the Virtual Mermory Page Mechanisam. So, our solution is execute some codes in every min
            //which may prevent sysetem uninstall memory from RAM to disk.

            System.Timers.Timer t = new System.Timers.Timer(1000 * 60 * 5) { AutoReset = true, Enabled = true };
            t.Elapsed += (o, e) => Dispatcher.Invoke(new Action(() =>
                {
                    if (Visibility != Visibility.Visible)
                    {
                        double oldLeft = Left;
                        Left = 20000;
                        ShowWox();
                        CommandFactory.DispatchCommand(new Query("qq"), false);
                        HideWox();
                        Left = oldLeft;
                    }
                }));
        }

        private void InitProgressbarAnimation()
        {
            DoubleAnimation da = new DoubleAnimation(progressBar.X2, Width + 100, new Duration(new TimeSpan(0, 0, 0, 0, 1600)));
            DoubleAnimation da1 = new DoubleAnimation(progressBar.X1, Width, new Duration(new TimeSpan(0, 0, 0, 0, 1600)));
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
            System.Windows.Forms.MenuItem open = new System.Windows.Forms.MenuItem("Open");
            open.Click += (o, e) => ShowWox();
            System.Windows.Forms.MenuItem exit = new System.Windows.Forms.MenuItem("Exit");
            exit.Click += (o, e) => CloseApp();
            System.Windows.Forms.MenuItem[] childen = { open, exit };
            notifyIcon.ContextMenu = new System.Windows.Forms.ContextMenu(childen);
        }

        private void OnHotKey(object sender, KeyPressedEventArgs e)
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

        private void TextBoxBase_OnTextChanged(object sender, TextChangedEventArgs e)
        {
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
                   }, TimeSpan.FromMilliseconds(30), null);
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
                       }, TimeSpan.FromSeconds(1), tbQuery.Text);
                   }

               }, TimeSpan.FromMilliseconds(150));
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

                    case "starthide":
                        HideApp();
                        break;
                }
            }
        }

        private void MainWindow_OnLoaded(object sender, RoutedEventArgs e)
        {
            Left = (SystemParameters.PrimaryScreenWidth - ActualWidth) / 2;
            Top = (SystemParameters.PrimaryScreenHeight - ActualHeight) / 3;

            WakeupApp();
            Plugins.Init();

            keyboardListener.hookedKeyboardCallback += KListener_hookedKeyboardCallback;
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
                    keyboardSimulator.ModifiedKeyStroke(WindowsInput.Native.VirtualKeyCode.LWIN, WindowsInput.Native.VirtualKeyCode.CONTROL);
                    return false;
                }
            }
            return true;
        }

        private void OnWinRPressed()
        {
            ShowWox(false);
            if (tbQuery.Text != ">")
            {
                resultCtrl.Clear();
                ChangeQuery(">");
            }
            tbQuery.CaretIndex = tbQuery.Text.Length;
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
                    e.Handled = true;
                    break;

                case Key.Up:
                    resultCtrl.SelectPrev();
                    e.Handled = true;
                    break;

                case Key.Enter:
                    AcceptSelect();
                    e.Handled = true;
                    break;
            }
        }

        private void AcceptSelect()
        {
            Result result = resultCtrl.AcceptSelect();
            if (result != null)
            {
                CommonStorage.Instance.UserSelectedRecords.Add(result);
                if (!result.DontHideWoxAfterSelect)
                {
                    HideWox();
                }
            }
        }

        public void OnUpdateResultView(List<Result> list)
        {
            queryHasReturn = true;
            progressBar.Dispatcher.Invoke(new Action(StopProgress));
            if (list.Count > 0)
            {
                //todo:this should be opened to users, it's their choise to use it or not in thier workflows
                list.ForEach(o =>
                {
                    if (o.AutoAjustScore) o.Score += CommonStorage.Instance.UserSelectedRecords.GetSelectedCount(o);
                });
                resultCtrl.Dispatcher.Invoke(new Action(() =>
                {
                    var t1 = Environment.TickCount;
                    List<Result> l = list.Where(o => o.OriginQuery != null && o.OriginQuery.RawQuery == tbQuery.Text).ToList();
                    resultCtrl.AddResults(l);
                    Debug.WriteLine("Time:" + (Environment.TickCount - t1) + " Count:" + l.Count);
                }));
            }
        }

        public void SetTheme(string themeName)
        {
            ResourceDictionary dict = new ResourceDictionary
            {
                Source = new Uri("pack://application:,,,/Themes/" + themeName + ".xaml")
            };

            Application.Current.Resources.MergedDictionaries.Clear();
            Application.Current.Resources.MergedDictionaries.Add(dict);
        }

        #region Public API

        //Those method can be invoked by plugins

        public void ChangeQuery(string query)
        {
            tbQuery.Text = query;
            tbQuery.CaretIndex = tbQuery.Text.Length;
        }

        public void CloseApp()
        {
            notifyIcon.Visible = false;
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
            Msg m = new Msg { Owner = GetWindow(this) };
            m.Show(title, subTitle, iconPath);
        }

        public void OpenSettingDialog()
        {
            SettingWidow s = new SettingWidow(this);
            s.Show();
        }

        #endregion
    }
}