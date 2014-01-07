using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using IWshRuntimeLibrary;
using Microsoft.Win32;
using WinAlfred.Commands;
using WinAlfred.Helper;
using WinAlfred.Plugin;
using WinAlfred.PluginLoader;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using MessageBox = System.Windows.MessageBox;
using Timer = System.Threading.Timer;

namespace WinAlfred
{
    public partial class MainWindow
    {
        private KeyboardHook hook = new KeyboardHook();
        private NotifyIcon notifyIcon;
        private Command cmdDispatcher;
        Storyboard progressBarStoryboard = new Storyboard();

        public MainWindow()
        {
            InitializeComponent();

            hook.KeyPressed += OnHotKey;
            hook.RegisterHotKey(XModifierKeys.Alt, Keys.Space);
            resultCtrl.resultItemChangedEvent += resultCtrl_resultItemChangedEvent;
            ThreadPool.SetMaxThreads(30, 10);
            InitProgressbarAnimation();
        }

        private void InitProgressbarAnimation()
        {
            DoubleAnimation da = new DoubleAnimation(progressBar.X2, Width + 100, new Duration(new TimeSpan(0, 0, 0,0,1600)));
            DoubleAnimation da1 = new DoubleAnimation(progressBar.X1, Width, new Duration(new TimeSpan(0, 0, 0, 0,1600)));
            Storyboard.SetTargetProperty(da, new PropertyPath("(Line.X1)"));
            Storyboard.SetTargetProperty(da1, new PropertyPath("(Line.X2)"));
            progressBarStoryboard.Children.Add(da);
            progressBarStoryboard.Children.Add(da1);
            progressBarStoryboard.RepeatBehavior = RepeatBehavior.Forever;
            progressBar.Visibility = Visibility.Hidden;
            progressBar.BeginStoryboard(progressBarStoryboard);
        }

        private void InitialTray()
        {
            notifyIcon = new NotifyIcon { Text = "WinAlfred", Icon = Properties.Resources.app, Visible = true };
            notifyIcon.Click += (o, e) => ShowWinAlfred();
            System.Windows.Forms.MenuItem open = new System.Windows.Forms.MenuItem("Open");
            open.Click += (o, e) => ShowWinAlfred();
            System.Windows.Forms.MenuItem exit = new System.Windows.Forms.MenuItem("Exit");
            exit.Click += (o, e) => CloseApp();
            System.Windows.Forms.MenuItem[] childen = { open, exit };
            notifyIcon.ContextMenu = new System.Windows.Forms.ContextMenu(childen);
        }

        private void resultCtrl_resultItemChangedEvent()
        {
            Height = resultCtrl.pnlContainer.ActualHeight + tbQuery.Height + tbQuery.Margin.Top + tbQuery.Margin.Bottom;
            resultCtrl.Margin = resultCtrl.GetCurrentResultCount() > 0 ? new Thickness { Bottom = 10, Left = 10, Right = 10 } : new Thickness { Bottom = 0, Left = 10, Right = 10 };
        }

        private void OnHotKey(object sender, KeyPressedEventArgs e)
        {
            if (!IsVisible)
            {
                ShowWinAlfred();
            }
            else
            {
                HideWinAlfred();
            }
        }

        private void TextBoxBase_OnTextChanged(object sender, TextChangedEventArgs e)
        {
            resultCtrl.Dirty = true;
            Dispatcher.DelayInvoke("UpdateSearch",
               () =>
               {
                   resultCtrl.Clear();
                   var q = new Query(tbQuery.Text);
                   cmdDispatcher.DispatchCommand(q);
               }, TimeSpan.FromMilliseconds(300));

        }


        private void StartProgress()
        {
            progressBar.Visibility = Visibility.Visible;
        }

        private void StopProgress()
        {
            progressBar.Visibility = Visibility.Hidden;
        }

        private void HideWinAlfred()
        {
            Hide();
        }

        private void ShowWinAlfred()
        {
            Show();
            //FocusManager.SetFocusedElement(this, tbQuery);
            tbQuery.Focusable = true;
            Keyboard.Focus(tbQuery);
            tbQuery.SelectAll();

            if (!tbQuery.IsKeyboardFocused)
            {
                MessageBox.Show("didnt focus");
            }
        }

        public void SetAutoStart(bool IsAtuoRun)
        {
            string LnkPath = Environment.GetFolderPath(Environment.SpecialFolder.Startup) + "//WinAlfred.lnk";
            if (IsAtuoRun)
            {
                WshShell shell = new WshShell();
                IWshShortcut shortcut = (IWshShortcut)shell.CreateShortcut(LnkPath);
                shortcut.TargetPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
                shortcut.WorkingDirectory = Environment.CurrentDirectory;
                shortcut.WindowStyle = 1; //normal window
                shortcut.Description = "WinAlfred";
                shortcut.Save();
            }
            else
            {
                System.IO.File.Delete(LnkPath);
            }
        }

        private void MainWindow_OnLoaded(object sender, RoutedEventArgs e)
        {
            Plugins.Init(this);
            cmdDispatcher = new Command(this);
            InitialTray();
            SetAutoStart(true);
            //var engine = new Jurassic.ScriptEngine();
            //MessageBox.Show(engine.Evaluate("5 * 10 + 2").ToString());
            StartProgress();
        }

        private void TbQuery_OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Escape:
                    HideWinAlfred();
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
                    if (resultCtrl.AcceptSelect()) HideWinAlfred();
                    e.Handled = true;
                    break;
            }
        }

        public void OnUpdateResultView(List<Result> list)
        {
            resultCtrl.Dispatcher.Invoke(new Action(() =>
            {
                List<Result> l = list.Where(o => o.OriginQuery != null && o.OriginQuery.RawQuery == tbQuery.Text).OrderByDescending(o => o.Score).ToList();
                resultCtrl.AddResults(l);
            }));
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
            Close();
        }

        public void HideApp()
        {
            HideWinAlfred();
        }

        public void ShowApp()
        {
            ShowWinAlfred();
        }

        public void ShowMsg(string title, string subTitle, string iconPath)
        {
            Msg m = new Msg { Owner = GetWindow(this) };
            m.Show(title, subTitle, iconPath);
        }

        #endregion
    }
}