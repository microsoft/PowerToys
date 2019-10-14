using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Navigation;
using Microsoft.Win32;
using NHotkey;
using NHotkey.Wpf;
using Wox.Core;
using Wox.Core.Plugin;
using Wox.Core.Resource;
using Wox.Infrastructure.Hotkey;
using Wox.Infrastructure.UserSettings;
using Wox.Plugin;
using Wox.ViewModel;

namespace Wox
{
    public partial class SettingWindow
    {
        private const string StartupPath = "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run";

        public readonly IPublicAPI _api;
        private Settings _settings;
        private SettingWindowViewModel _viewModel;

        public SettingWindow(IPublicAPI api, SettingWindowViewModel viewModel)
        {
            InitializeComponent();
            _settings = viewModel.Settings;
            DataContext = viewModel;
            _viewModel = viewModel;
            _api = api;
        }

        #region General

        void OnLanguageChanged(object sender, SelectionChangedEventArgs e)
        {
            var language = (Language)e.AddedItems[0];
            InternationalizationManager.Instance.ChangeLanguage(language);
        }

        private void OnAutoStartupChecked(object sender, RoutedEventArgs e)
        {
            SetStartup();
        }

        private void OnAutoStartupUncheck(object sender, RoutedEventArgs e)
        {
            RemoveStartup();
        }

        public static void SetStartup()
        {
            using (var key = Registry.CurrentUser.OpenSubKey(StartupPath, true))
            {
                key?.SetValue(Infrastructure.Constant.Wox, Infrastructure.Constant.ExecutablePath);
            }
        }

        private void RemoveStartup()
        {
            using (var key = Registry.CurrentUser.OpenSubKey(StartupPath, true))
            {
                key?.DeleteValue(Infrastructure.Constant.Wox, false);
            }
        }

        public static bool StartupSet()
        {
            using (var key = Registry.CurrentUser.OpenSubKey(StartupPath, true))
            {
                var path = key?.GetValue(Infrastructure.Constant.Wox) as string;
                if (path != null)
                {
                    return path == Infrastructure.Constant.ExecutablePath;
                }
                else
                {
                    return false;
                }
            }
        }

        private void OnSelectPythonDirectoryClick(object sender, RoutedEventArgs e)
        {
            var dlg = new System.Windows.Forms.FolderBrowserDialog
            {
                SelectedPath = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles)
            };

            var result = dlg.ShowDialog();
            if (result == System.Windows.Forms.DialogResult.OK)
            {
                string pythonDirectory = dlg.SelectedPath;
                if (!string.IsNullOrEmpty(pythonDirectory))
                {
                    var pythonPath = Path.Combine(pythonDirectory, PluginsLoader.PythonExecutable);
                    if (File.Exists(pythonPath))
                    {
                        _settings.PluginSettings.PythonDirectory = pythonDirectory;
                        MessageBox.Show("Remember to restart Wox use new Python path");
                    }
                    else
                    {
                        MessageBox.Show("Can't find python in given directory");
                    }
                }
            }
        }

        #endregion

        #region Hotkey

        private void OnHotkeyControlLoaded(object sender, RoutedEventArgs e)
        {
            HotkeyControl.SetHotkey(_viewModel.Settings.Hotkey, false);
        }

        void OnHotkeyChanged(object sender, EventArgs e)
        {
            if (HotkeyControl.CurrentHotkeyAvailable)
            {
                SetHotkey(HotkeyControl.CurrentHotkey, (o, args) =>
                {
                    if (!Application.Current.MainWindow.IsVisible)
                    {
                        Application.Current.MainWindow.Visibility = Visibility.Visible;
                    }
                    else
                    {
                        Application.Current.MainWindow.Visibility = Visibility.Hidden;
                    }
                });
                RemoveHotkey(_settings.Hotkey);
                _settings.Hotkey = HotkeyControl.CurrentHotkey.ToString();
            }
        }

        void SetHotkey(HotkeyModel hotkey, EventHandler<HotkeyEventArgs> action)
        {
            string hotkeyStr = hotkey.ToString();
            try
            {
                HotkeyManager.Current.AddOrReplace(hotkeyStr, hotkey.CharKey, hotkey.ModifierKeys, action);
            }
            catch (Exception)
            {
                string errorMsg =
                    string.Format(InternationalizationManager.Instance.GetTranslation("registerHotkeyFailed"), hotkeyStr);
                MessageBox.Show(errorMsg);
            }
        }

        void RemoveHotkey(string hotkeyStr)
        {
            if (!string.IsNullOrEmpty(hotkeyStr))
            {
                HotkeyManager.Current.Remove(hotkeyStr);
            }
        }

        private void OnDeleteCustomHotkeyClick(object sender, RoutedEventArgs e)
        {
            var item = _viewModel.SelectedCustomPluginHotkey;
            if (item == null)
            {
                MessageBox.Show(InternationalizationManager.Instance.GetTranslation("pleaseSelectAnItem"));
                return;
            }

            string deleteWarning =
                string.Format(InternationalizationManager.Instance.GetTranslation("deleteCustomHotkeyWarning"),
                    item.Hotkey);
            if (
                MessageBox.Show(deleteWarning, InternationalizationManager.Instance.GetTranslation("delete"),
                    MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                _settings.CustomPluginHotkeys.Remove(item);
                RemoveHotkey(item.Hotkey);
            }
        }

        private void OnnEditCustomHotkeyClick(object sender, RoutedEventArgs e)
        {
            var item = _viewModel.SelectedCustomPluginHotkey;
            if (item != null)
            {
                CustomQueryHotkeySetting window = new CustomQueryHotkeySetting(this, _settings);
                window.UpdateItem(item);
                window.ShowDialog();
            }
            else
            {
                MessageBox.Show(InternationalizationManager.Instance.GetTranslation("pleaseSelectAnItem"));
            }
        }

        private void OnAddCustomeHotkeyClick(object sender, RoutedEventArgs e)
        {
            new CustomQueryHotkeySetting(this, _settings).ShowDialog();
        }

        #endregion

        #region Plugin

        private void OnPluginToggled(object sender, RoutedEventArgs e)
        {
            var id = _viewModel.SelectedPlugin.PluginPair.Metadata.ID;
            _settings.PluginSettings.Plugins[id].Disabled = _viewModel.SelectedPlugin.PluginPair.Metadata.Disabled;
        }

        private void OnPluginActionKeywordsClick(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                var id = _viewModel.SelectedPlugin.PluginPair.Metadata.ID;
                ActionKeywords changeKeywordsWindow = new ActionKeywords(id, _settings);
                changeKeywordsWindow.ShowDialog();
            }
        }

        private void OnPluginNameClick(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                var website = _viewModel.SelectedPlugin.PluginPair.Metadata.Website;
                if (!string.IsNullOrEmpty(website))
                {
                    var uri = new Uri(website);
                    if (Uri.CheckSchemeName(uri.Scheme))
                    {
                        Process.Start(website);
                    }
                }
            }
        }

        private void OnPluginDirecotyClick(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                var directory = _viewModel.SelectedPlugin.PluginPair.Metadata.PluginDirectory;
                if (!string.IsNullOrEmpty(directory) && Directory.Exists(directory))
                {
                    Process.Start(directory);
                }
            }
        }
        #endregion

        #region Proxy

        private void OnTestProxyClick(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_settings.Proxy.Server))
            {
                MessageBox.Show(InternationalizationManager.Instance.GetTranslation("serverCantBeEmpty"));
                return;
            }
            if (_settings.Proxy.Port <= 0)
            {
                MessageBox.Show(InternationalizationManager.Instance.GetTranslation("portCantBeEmpty"));
                return;
            }

            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(Infrastructure.Constant.Repository);
            if (string.IsNullOrEmpty(_settings.Proxy.UserName) || string.IsNullOrEmpty(_settings.Proxy.Password))
            {
                request.Proxy = new WebProxy(_settings.Proxy.Server, _settings.Proxy.Port);
            }
            else
            {
                request.Proxy = new WebProxy(_settings.Proxy.Server, _settings.Proxy.Port)
                {
                    Credentials = new NetworkCredential(_settings.Proxy.UserName, _settings.Proxy.Password)
                };
            }
            try
            {
                var response = (HttpWebResponse)request.GetResponse();
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    MessageBox.Show(InternationalizationManager.Instance.GetTranslation("proxyIsCorrect"));
                }
                else
                {
                    MessageBox.Show(InternationalizationManager.Instance.GetTranslation("proxyConnectFailed"));
                }
            }
            catch
            {
                MessageBox.Show(InternationalizationManager.Instance.GetTranslation("proxyConnectFailed"));
            }
        }

        #endregion

        private async void OnCheckUpdates(object sender, RoutedEventArgs e)
        {
            await Updater.UpdateApp();
        }

        private void OnRequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri));
            e.Handled = true;
        }

        private void OnClosed(object sender, EventArgs e)
        {
            _viewModel.Save();
            PluginManager.Save();
        }

        private void OnCloseExecuted(object sender, ExecutedRoutedEventArgs e)
        {
            Close();
        }
    }
}
