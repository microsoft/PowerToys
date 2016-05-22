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
using Wox.Core.UserSettings;
using Wox.Infrastructure.Hotkey;
using Wox.Plugin;
using Wox.ViewModel;

namespace Wox
{
    public partial class SettingWindow
    {
        private const string StartupPath = "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run";

        public readonly IPublicAPI _api;
        bool settingsLoaded;
        private bool themeTabLoaded;
        private Settings _settings;
        private SettingWindowViewModel _viewModel;

        public SettingWindow(IPublicAPI api, SettingWindowViewModel viewModel)
        {
            InitializeComponent();
            _settings = viewModel.Settings;
            DataContext = viewModel;
            _viewModel = viewModel;
            _api = api;
            Loaded += Setting_Loaded;
        }

        private void ProxyToggled(object sender, RoutedEventArgs e)
        {
            _settings.ProxyEnabled = ToggleProxy.IsChecked ?? false;
        }

        private void Setting_Loaded(object sender, RoutedEventArgs ev)
        {
            #region Proxy

            ToggleProxy.IsChecked = _settings.ProxyEnabled;
            ProxyServer.Text = _settings.ProxyServer;
            if (_settings.ProxyPort != 0)
            {
                ProxyPort.Text = _settings.ProxyPort.ToString();
            }
            ProxyUserName.Text = _settings.ProxyUserName;
            ProxyPassword.Password = _settings.ProxyPassword;

            #endregion

            #region About

            string activateTimes = string.Format(
                InternationalizationManager.Instance.GetTranslation("about_activate_times"), _settings.ActivateTimes);
            ActivatedTimes.Text = activateTimes;
            Version.Text = Infrastructure.Constant.Version;

            #endregion

            settingsLoaded = true;
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
                SetHotkey(HotkeyControl.CurrentHotkey, delegate
                {
                    if (!Application.Current.MainWindow.IsVisible)
                    {
                        _api.ShowApp();
                    }
                    else
                    {
                        _api.HideApp();
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

        #region Theme

        private void OnMoreThemesClick(object sender, MouseButtonEventArgs e)
        {
            Process.Start("http://www.getwox.com/theme");
        }
        #endregion

        #region Plugin

        private void OnPluginToggled(object sender, RoutedEventArgs e)
        {
            var id = _viewModel.SelectedPlugin.Metadata.ID;
            _settings.PluginSettings.Plugins[id].Disabled = _viewModel.SelectedPlugin.Metadata.Disabled;
        }

        private void OnPluginActionKeywordsClick(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                var id = _viewModel.SelectedPlugin.Metadata.ID;
                ActionKeywords changeKeywordsWindow = new ActionKeywords(id, _settings);
                changeKeywordsWindow.ShowDialog();
            }
        }

        private void OnPluginNameClick(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                if (e.ChangedButton == MouseButton.Left)
                {
                    var website = _viewModel.SelectedPlugin.Metadata.Website;
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
        }

        private void OnPluginDirecotyClick(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                var directory = _viewModel.SelectedPlugin.Metadata.PluginDirectory;
                if (!string.IsNullOrEmpty(directory) && Directory.Exists(directory))
                {
                    Process.Start(directory);
                }
            }
        }

        private void OnMorePluginsClicked(object sender, MouseButtonEventArgs e)
        {
            Process.Start("http://www.getwox.com/plugin");
        }

        #endregion

        #region Proxy

        private void btnSaveProxy_Click(object sender, RoutedEventArgs e)
        {
            _settings.ProxyEnabled = ToggleProxy.IsChecked ?? false;

            int port = 80;
            if (_settings.ProxyEnabled)
            {
                if (string.IsNullOrEmpty(ProxyServer.Text))
                {
                    MessageBox.Show(InternationalizationManager.Instance.GetTranslation("serverCantBeEmpty"));
                    return;
                }
                if (string.IsNullOrEmpty(ProxyPort.Text))
                {
                    MessageBox.Show(InternationalizationManager.Instance.GetTranslation("portCantBeEmpty"));
                    return;
                }
                if (!int.TryParse(ProxyPort.Text, out port))
                {
                    MessageBox.Show(InternationalizationManager.Instance.GetTranslation("invalidPortFormat"));
                    return;
                }
            }

            _settings.ProxyServer = ProxyServer.Text;
            _settings.ProxyPort = port;
            _settings.ProxyUserName = ProxyUserName.Text;
            _settings.ProxyPassword = ProxyPassword.Password;

            MessageBox.Show(InternationalizationManager.Instance.GetTranslation("saveProxySuccessfully"));
        }

        private void btnTestProxy_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(ProxyServer.Text))
            {
                MessageBox.Show(InternationalizationManager.Instance.GetTranslation("serverCantBeEmpty"));
                return;
            }
            if (string.IsNullOrEmpty(ProxyPort.Text))
            {
                MessageBox.Show(InternationalizationManager.Instance.GetTranslation("portCantBeEmpty"));
                return;
            }
            int port;
            if (!int.TryParse(ProxyPort.Text, out port))
            {
                MessageBox.Show(InternationalizationManager.Instance.GetTranslation("invalidPortFormat"));
                return;
            }

            HttpWebRequest request = (HttpWebRequest)WebRequest.Create("http://www.baidu.com");
            request.Timeout = 1000 * 5;
            request.ReadWriteTimeout = 1000 * 5;
            if (string.IsNullOrEmpty(ProxyUserName.Text))
            {
                request.Proxy = new WebProxy(ProxyServer.Text, port);
            }
            else
            {
                request.Proxy = new WebProxy(ProxyServer.Text, port);
                request.Proxy.Credentials = new NetworkCredential(ProxyUserName.Text, ProxyPassword.Password);
            }
            try
            {
                HttpWebResponse response = (HttpWebResponse)request.GetResponse();
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

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            // Hide window with ESC, but make sure it is not pressed as a hotkey
            if (e.Key == Key.Escape && !HotkeyControl.IsFocused)
            {
                Close();
            }
        }

        private async void OnCheckUpdates(object sender, RoutedEventArgs e)
        {
            var version = await Updater.NewVersion();
            if (!string.IsNullOrEmpty(version))
            {
                var newVersion = Updater.NumericVersion(version);
                var oldVersion = Updater.NumericVersion(Infrastructure.Constant.Version);
                if (newVersion > oldVersion)
                {
                    NewVersionTips.Text = string.Format(NewVersionTips.Text, version);
                    NewVersionTips.Visibility = Visibility.Visible;
                    Updater.UpdateApp();
                }
            }
        }

        private void OnRequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri));
            e.Handled = true;
        }


    }
}
