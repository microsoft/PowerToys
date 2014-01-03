using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Threading;
using WinAlfred.Commands;
using WinAlfred.Helper;
using WinAlfred.Plugin;
using WinAlfred.PluginLoader;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;

namespace WinAlfred
{
    public partial class MainWindow : Window
    {
        private KeyboardHook hook = new KeyboardHook();
        private List<Result> results = new List<Result>();
        private NotifyIcon notifyIcon = null;
        private CommandDispatcher cmdDispatcher = new CommandDispatcher();

        public MainWindow()
        {
            InitializeComponent();
            hook.KeyPressed += OnHotKey;
            hook.RegisterHotKey(XModifierKeys.Alt, Keys.Space);
            resultCtrl.resultItemChangedEvent += resultCtrl_resultItemChangedEvent;
            ThreadPool.SetMaxThreads(10, 5);
        }

        private void InitialTray()
        {
            notifyIcon = new NotifyIcon { Text = "WinAlfred", Icon = Properties.Resources.app, Visible = true };
            notifyIcon.Click += (o, e) => ShowWinAlfred();
            System.Windows.Forms.MenuItem open = new System.Windows.Forms.MenuItem("Open");
            open.Click += (o, e) => ShowWinAlfred();
            System.Windows.Forms.MenuItem exit = new System.Windows.Forms.MenuItem("Exit");
            exit.Click += (o, e) =>
            {
                notifyIcon.Visible = false;
                Close();
            };
            System.Windows.Forms.MenuItem[] childen = { open, exit };
            notifyIcon.ContextMenu = new System.Windows.Forms.ContextMenu(childen);
        }

        private void resultCtrl_resultItemChangedEvent()
        {
            Height = resultCtrl.pnlContainer.ActualHeight + tbQuery.Height + tbQuery.Margin.Top + tbQuery.Margin.Bottom;
            resultCtrl.Margin = results.Count > 0 ? new Thickness { Bottom = 10, Left = 10, Right = 10 } : new Thickness { Bottom = 0, Left = 10, Right = 10 };
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
            var q = new Query(tbQuery.Text);
            cmdDispatcher.DispatchCommand(q);
        }

        private void HideWinAlfred()
        {
            Hide();
        }

        private void ShowWinAlfred()
        {
            tbQuery.SelectAll();
            Show();
            Focus();
            FocusManager.SetFocusedElement(this, tbQuery);
        }

        private void MainWindow_OnLoaded(object sender, RoutedEventArgs e)
        {
            Plugins.Init();
            ShowWinAlfred();
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
                    resultCtrl.AcceptSelect();
                    HideWinAlfred();
                    e.Handled = true;
                    break;
            }
        }
    }
}