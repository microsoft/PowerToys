using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Input;
using WinAlfred.Helper;
using WinAlfred.Plugin;
using WinAlfred.PluginLoader;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;

namespace WinAlfred
{
    public partial class MainWindow : Window
    {
        private KeyboardHook hook = new KeyboardHook();
        public List<PluginPair> plugins = new List<PluginPair>();
        private List<Result> results = new List<Result>();

        public MainWindow()
        {
            InitializeComponent();
            hook.KeyPressed += OnHotKey;
            hook.RegisterHotKey(XModifierKeys.Alt, Keys.Space);
            resultCtrl.resultItemChangedEvent += resultCtrl_resultItemChangedEvent;
        }

        private void resultCtrl_resultItemChangedEvent()
        {
            Height = resultCtrl.pnlContainer.ActualHeight + tbQuery.Height + tbQuery.Margin.Top + tbQuery.Margin.Bottom;
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
            results.Clear();
            foreach (PluginPair pair in plugins)
            {
                var q = new Query(tbQuery.Text);
                if (pair.Metadata.ActionKeyword == q.ActionName)
                {
                    try
                    {
                        results.AddRange(pair.Plugin.Query(q));
                        results.ForEach(o => o.PluginDirectory = pair.Metadata.PluginDirecotry);
                    }
                    catch (Exception queryException)
                    {
                        Log.Error(string.Format("Plugin {0} query failed: {1}", pair.Metadata.Name,
                            queryException.Message));
#if (DEBUG)
                        {
                            throw;
                        }
#endif
                    }
                }
            }
            resultCtrl.AddResults(results.OrderByDescending(o => o.Score).ToList());
            resultCtrl.SelectFirst();
        }

        public void HideWinAlfred()
        {
            Hide();
        }

        public void ShowWinAlfred()
        {
            tbQuery.SelectAll();
            Focus();
            tbQuery.Focus();
            Show();
        }

        private void MainWindow_OnLoaded(object sender, RoutedEventArgs e)
        {
            plugins.AddRange(new PythonPluginLoader().LoadPlugin());
            plugins.AddRange(new CSharpPluginLoader().LoadPlugin());

            ShowWinAlfred();
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
                    e.Handled = true;
                    break;
            }
        }
    }
}