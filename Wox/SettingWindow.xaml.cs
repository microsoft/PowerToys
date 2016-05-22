using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using Microsoft.Win32;
using NHotkey;
using NHotkey.Wpf;
using Wox.Core;
using Wox.Core.Plugin;
using Wox.Core.Resource;
using Wox.Core.UserSettings;
using Wox.Helper;
using Wox.Infrastructure.Hotkey;
using Wox.Plugin;
using Wox.ViewModel;
using Stopwatch = Wox.Infrastructure.Stopwatch;

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
            ResultListBoxPreview.DataContext = new ResultsViewModel(_settings);
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

        void ctlHotkey_OnHotkeyChanged(object sender, EventArgs e)
        {
            if (HotkeyControl.CurrentHotkeyAvailable)
            {
                SetHotkey(HotkeyControl.CurrentHotkey, delegate
                {
                    if (!System.Windows.Application.Current.MainWindow.IsVisible)
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

        public void OnHotkeyTabSelected(object sender, RoutedEventArgs e)
        {
            HotkeyControl.HotkeyChanged += ctlHotkey_OnHotkeyChanged;
            HotkeyControl.SetHotkey(_settings.Hotkey, false);
            CustomHotkies.ItemsSource = _settings.CustomPluginHotkeys;
        }

        private void BtnDeleteCustomHotkey_OnClick(object sender, RoutedEventArgs e)
        {
            CustomPluginHotkey item = CustomHotkies.SelectedItem as CustomPluginHotkey;
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
                CustomHotkies.Items.Refresh();
                RemoveHotkey(item.Hotkey);
            }
        }

        private void BtnEditCustomHotkey_OnClick(object sender, RoutedEventArgs e)
        {
            CustomPluginHotkey item = CustomHotkies.SelectedItem as CustomPluginHotkey;
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

        private void BtnAddCustomeHotkey_OnClick(object sender, RoutedEventArgs e)
        {
            new CustomQueryHotkeySetting(this, _settings).ShowDialog();
        }

        public void ReloadCustomPluginHotkeyView()
        {
            CustomHotkies.Items.Refresh();
        }

        #endregion

        #region Theme

        private void tbMoreThemes_MouseUp(object sender, MouseButtonEventArgs e)
        {
            Process.Start("http://www.getwox.com/theme");
        }

        public void OnThemeTabSelected(object sender, RoutedEventArgs e)
        {
            Stopwatch.Debug("theme load", () =>
            {
                var s = Fonts.SystemFontFamilies;
            });

            if (themeTabLoaded) return;

            themeTabLoaded = true;
            if (!string.IsNullOrEmpty(_settings.QueryBoxFont) &&
                Fonts.SystemFontFamilies.Count(o => o.FamilyNames.Values.Contains(_settings.QueryBoxFont)) > 0)
            {
                QueryBoxFont.Text = _settings.QueryBoxFont;

                QueryBoxFontFaces.SelectedItem =
                    SyntaxSugars.CallOrRescueDefault(
                        () => ((FontFamily)QueryBoxFont.SelectedItem).ConvertFromInvariantStringsOrNormal(
                            _settings.QueryBoxFontStyle,
                            _settings.QueryBoxFontWeight,
                            _settings.QueryBoxFontStretch
                            ));
            }
            if (!string.IsNullOrEmpty(_settings.ResultFont) &&
                Fonts.SystemFontFamilies.Count(o => o.FamilyNames.Values.Contains(_settings.ResultFont)) > 0)
            {
                ResultFontComboBox.Text = _settings.ResultFont;

                ResultFontFacesComboBox.SelectedItem =
                    SyntaxSugars.CallOrRescueDefault(
                        () => ((FontFamily)ResultFontComboBox.SelectedItem).ConvertFromInvariantStringsOrNormal(
                            _settings.ResultFontStyle,
                            _settings.ResultFontWeight,
                            _settings.ResultFontStretch
                            ));
            }

            ResultListBoxPreview.AddResults(new List<Result>
            {
                new Result
                {
                    Title = "Wox is an effective launcher for windows",
                    SubTitle = "Wox provide bundles of features let you access infomations quickly.",
                    IcoPath = "Images/app.png",
                    PluginDirectory = Path.GetDirectoryName(Infrastructure.Constant.ProgramDirectory)
                },
                new Result
                {
                    Title = "Search applications",
                    SubTitle = "Search applications, files (via everything plugin) and browser bookmarks",
                    IcoPath = "Images/app.png",
                    PluginDirectory = Path.GetDirectoryName(Infrastructure.Constant.ProgramDirectory)
                },
                new Result
                {
                    Title = "Search web contents with shortcuts",
                    SubTitle = "e.g. search google with g keyword or youtube keyword)",
                    IcoPath = "Images/app.png",
                    PluginDirectory = Path.GetDirectoryName(Infrastructure.Constant.ProgramDirectory)
                },
                new Result
                {
                    Title = "clipboard history ",
                    IcoPath = "Images/app.png",
                    PluginDirectory = Path.GetDirectoryName(Infrastructure.Constant.ProgramDirectory)
                },
                new Result
                {
                    Title = "Themes support",
                    SubTitle = "get more themes from http://www.getwox.com/theme",
                    IcoPath = "Images/app.png",
                    PluginDirectory = Path.GetDirectoryName(Infrastructure.Constant.ProgramDirectory)
                },
                new Result
                {
                    Title = "Plugins support",
                    SubTitle = "get more plugins from http://www.getwox.com/plugin",
                    IcoPath = "Images/app.png",
                    PluginDirectory = Path.GetDirectoryName(Infrastructure.Constant.ProgramDirectory)
                },
                new Result
                {
                    Title = "Wox is an open-source software",
                    SubTitle = "Wox benefits from the open-source community a lot",
                    IcoPath = "Images/app.png",
                    PluginDirectory = Path.GetDirectoryName(Infrastructure.Constant.ProgramDirectory)
                }
            });

            foreach (string theme in ThemeManager.Instance.LoadAvailableThemes())
            {
                string themeName = Path.GetFileNameWithoutExtension(theme);
                Theme.Items.Add(themeName);
            }

            Theme.SelectedItem = _settings.Theme;

            var wallpaper = WallpaperPathRetrieval.GetWallpaperPath();
            if (wallpaper != null && File.Exists(wallpaper))
            {
                var memStream = new MemoryStream(File.ReadAllBytes(wallpaper));
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.StreamSource = memStream;
                bitmap.EndInit();
                var brush = new ImageBrush(bitmap);
                brush.Stretch = Stretch.UniformToFill;
                PreviewPanel.Background = brush;
            }
            else
            {
                var wallpaperColor = WallpaperPathRetrieval.GetWallpaperColor();
                PreviewPanel.Background = new SolidColorBrush(wallpaperColor);
            }

        }

        private void ThemeComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            string themeName = Theme.SelectedItem.ToString();
            ThemeManager.Instance.ChangeTheme(themeName);
        }

        private void CbQueryBoxFont_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!settingsLoaded) return;
            string queryBoxFontName = QueryBoxFont.SelectedItem.ToString();
            _settings.QueryBoxFont = queryBoxFontName;
            QueryBoxFontFaces.SelectedItem = ((FontFamily)QueryBoxFont.SelectedItem).ChooseRegularFamilyTypeface();
            ThemeManager.Instance.ChangeTheme(_settings.Theme);
        }

        private void CbQueryBoxFontFaces_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!settingsLoaded) return;
            FamilyTypeface typeface = (FamilyTypeface)QueryBoxFontFaces.SelectedItem;
            if (typeface == null)
            {
                if (QueryBoxFontFaces.Items.Count > 0)
                    QueryBoxFontFaces.SelectedIndex = 0;
            }
            else
            {
                _settings.QueryBoxFontStretch = typeface.Stretch.ToString();
                _settings.QueryBoxFontWeight = typeface.Weight.ToString();
                _settings.QueryBoxFontStyle = typeface.Style.ToString();
                ThemeManager.Instance.ChangeTheme(_settings.Theme);
            }
        }

        private void OnResultFontSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!settingsLoaded) return;
            string resultItemFont = ResultFontComboBox.SelectedItem.ToString();
            _settings.ResultFont = resultItemFont;
            ResultFontFacesComboBox.SelectedItem =
                ((FontFamily)ResultFontComboBox.SelectedItem).ChooseRegularFamilyTypeface();
            ThemeManager.Instance.ChangeTheme(_settings.Theme);
        }

        private void OnResultFontFacesSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!settingsLoaded) return;
            FamilyTypeface typeface = (FamilyTypeface)ResultFontFacesComboBox.SelectedItem;
            if (typeface == null)
            {
                if (ResultFontFacesComboBox.Items.Count > 0)
                    ResultFontFacesComboBox.SelectedIndex = 0;
            }
            else
            {
                _settings.ResultFontStretch = typeface.Stretch.ToString();
                _settings.ResultFontWeight = typeface.Weight.ToString();
                _settings.ResultFontStyle = typeface.Style.ToString();
                ThemeManager.Instance.ChangeTheme(_settings.Theme);
            }
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
