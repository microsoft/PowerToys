using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Input;
using Noesis.Javascript;
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

        public MainWindow()
        {
            InitializeComponent();

            hook.KeyPressed += OnHotKey;
            hook.RegisterHotKey(XModifierKeys.Alt, Keys.Space);
            resultCtrl.resultItemChangedEvent += resultCtrl_resultItemChangedEvent;
            ThreadPool.SetMaxThreads(30, 10);
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

            // Initialize a context
            using (JavascriptContext context = new JavascriptContext())
            {

                // Setting external parameters for the context
                context.SetParameter("message", "Hello World !");
                context.SetParameter("number", 1);

                // Script
                string script = @"
        var i;
        for (i = 0; i < 5; i++)
            console.Print(message + ' (' + i + ')');
        number += i;
    ";

                // Running the script
                context.Run(script);

                // Getting a parameter
                MessageBox.Show("number: " + context.GetParameter("number"));
            }

            //MessageBox.Show("s");
            resultCtrl.Dirty = true;
            ////auto clear results after 50ms if there are any results returned by plugins
            ////why we do this? because if we clear resulsts in the start of the text changed event
            ////we will see the splash. The more closer that clear and addResult method, the less splash we will see.
            new Timer(o =>
            {
                if (resultCtrl.Dirty)
                {
                    resultCtrl.Dispatcher.Invoke(new Action(() => resultCtrl.Clear()));
                }
            }, null, TimeSpan.FromMilliseconds(50), TimeSpan.FromMilliseconds(-1));
            if (string.IsNullOrEmpty(tbQuery.Text)) return;

            var q = new Query(tbQuery.Text);
            cmdDispatcher.DispatchCommand(q);
        }

        private void HideWinAlfred()
        {
            Hide();
        }

        private void ShowWinAlfred()
        {
            Show();
            //FocusManager.SetFocusedElement(this, tbQuery);
            Keyboard.Focus(tbQuery);
            tbQuery.SelectAll();
        }

        private void MainWindow_OnLoaded(object sender, RoutedEventArgs e)
        {
            Plugins.Init(this);
            cmdDispatcher = new Command(this);
            InitialTray();


          
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