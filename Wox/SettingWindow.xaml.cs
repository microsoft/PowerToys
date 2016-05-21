using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
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
using Wox.Infrastructure.Image;
using Wox.Infrastructure.Logger;
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
        private Dictionary<ISettingProvider, Control> featureControls = new Dictionary<ISettingProvider, Control>();
        private bool themeTabLoaded;
        private Settings _settings;

        public SettingWindow(IPublicAPI api, SettingWindowViewModel viewModel)
        {
            InitializeComponent();
            _settings = viewModel.Settings;
            DataContext = viewModel;
            _api = api;
            ResultListBoxPreview.DataContext = new ResultsViewModel(_settings);
            Loaded += Setting_Loaded;
        }

        private void ProxyToggled(object sender, RoutedEventArgs e)
        {
            _settings.ProxyEnabled = ToggleProxy.IsChecked ?? false;
        }

        private async void Setting_Loaded(object sender, RoutedEventArgs ev)
        {
            #region General

            HideOnStartup.Checked += (o, e) =>
            {
                _settings.HideOnStartup = true;
            };

            HideOnStartup.Unchecked += (o, e) =>
            {
                _settings.HideOnStartup = false;
            };

            HideWhenDeactive.Checked += (o, e) =>
            {
                _settings.HideWhenDeactive = true;
            };

            HideWhenDeactive.Unchecked += (o, e) =>
            {
                _settings.HideWhenDeactive = false;
            };

            RememberLastLocation.Checked += (o, e) =>
            {
                _settings.RememberLastLaunchLocation = true;
            };

            RememberLastLocation.Unchecked += (o, e) =>
            {
                _settings.RememberLastLaunchLocation = false;
            };

            IgnoreHotkeysOnFullscreen.Checked += (o, e) =>
            {
                _settings.IgnoreHotkeysOnFullscreen = true;
            };


            IgnoreHotkeysOnFullscreen.Unchecked += (o, e) =>
            {
                _settings.IgnoreHotkeysOnFullscreen = false;
            };

            AutoUpdates.Checked += (o, e) =>
            {
                _settings.AutoUpdates = true;
            };


            AutoUpdates.Unchecked += (o, e) =>
            {
                _settings.AutoUpdates = false;
            };


            AutoStartup.IsChecked = _settings.StartWoxOnSystemStartup;
            MaxResults.SelectionChanged += (o, e) =>
            {
                _settings.MaxResultsToShow = (int)MaxResults.SelectedItem;
                //MainWindow.pnlResult.lbResults.GetBindingExpression(MaxHeightProperty).UpdateTarget();
            };

            HideOnStartup.IsChecked = _settings.HideOnStartup;
            HideWhenDeactive.IsChecked = _settings.HideWhenDeactive;
            RememberLastLocation.IsChecked = _settings.RememberLastLaunchLocation;
            IgnoreHotkeysOnFullscreen.IsChecked = _settings.IgnoreHotkeysOnFullscreen;
            AutoUpdates.IsChecked = _settings.AutoUpdates;

            LoadLanguages();

            MaxResults.ItemsSource = Enumerable.Range(2, 16);
            var maxResults = _settings.MaxResultsToShow;
            MaxResults.SelectedItem = maxResults == 0 ? 6 : maxResults;

            PythonDirectory.Text = _settings.PluginSettings.PythonDirectory;

            #endregion

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

        public void SwitchTo(string tabName)
        {
            switch (tabName)
            {
                case "general":
                    SettingTab.SelectedIndex = 0;
                    break;
                case "plugin":
                    SettingTab.SelectedIndex = 1;
                    break;
                case "theme":
                    SettingTab.SelectedIndex = 2;
                    break;
                case "hotkey":
                    SettingTab.SelectedIndex = 3;
                    break;
                case "proxy":
                    SettingTab.SelectedIndex = 4;
                    break;
                case "about":
                    SettingTab.SelectedIndex = 5;
                    break;
            }
        }

        private void settingTab_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Update controls inside the selected tab
            if (e.OriginalSource != SettingTab) return;

            if (PluginTab.IsSelected)
            {
                OnPluginTabSelected();
            }
            else if (ThemeTab.IsSelected)
            {
                OnThemeTabSelected();
            }
            else if (HotkeyTab.IsSelected)
            {
                OnHotkeyTabSelected();
            }
        }

        #region General

        private void LoadLanguages()
        {
            Languages.ItemsSource = InternationalizationManager.Instance.LoadAvailableLanguages();
            Languages.DisplayMemberPath = "Display";
            Languages.SelectedValuePath = "LanguageCode";
            Languages.SelectedValue = _settings.Language;
            Languages.SelectionChanged += cbLanguages_SelectionChanged;
        }

        void cbLanguages_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            InternationalizationManager.Instance.ChangeLanguage(Languages.SelectedItem as Language);
        }

        private void CbStartWithWindows_OnChecked(object sender, RoutedEventArgs e)
        {
            SetStartup();
            _settings.StartWoxOnSystemStartup = true;
        }

        private void CbStartWithWindows_OnUnchecked(object sender, RoutedEventArgs e)
        {
            RemoveStartup();
            _settings.StartWoxOnSystemStartup = false;
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
                var path = key?.GetValue("Wox") as string;
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

        private void SelectPythonDirectoryOnClick(object sender, RoutedEventArgs e)
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
                        PythonDirectory.Text = pythonDirectory;
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

        private void OnHotkeyTabSelected()
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

        private void OnThemeTabSelected()
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

        private void lbPlugins_OnSelectionChanged(object sender, SelectionChangedEventArgs _)
        {

            var pair = PluginsListBox.SelectedItem as PluginPair;
            string pluginId = string.Empty;
            List<string> actionKeywords = null;
            if (pair == null) return;
            actionKeywords = pair.Metadata.ActionKeywords;
            PluginAuthor.Visibility = Visibility.Visible;
            PluginInitTime.Text =
                string.Format(InternationalizationManager.Instance.GetTranslation("plugin_init_time"), pair.InitTime);
            PluginQueryTime.Text =
                string.Format(InternationalizationManager.Instance.GetTranslation("plugin_query_time"),
                    pair.AvgQueryTime);
            if (actionKeywords.Count > 1)
            {
                PluginActionKeywordsTitle.Visibility = Visibility.Collapsed;
                PluginActionKeywords.Visibility = Visibility.Collapsed;
            }
            else
            {
                PluginActionKeywordsTitle.Visibility = Visibility.Visible;
                PluginActionKeywords.Visibility = Visibility.Visible;
            }
            OpenPluginDirecoty.Visibility = Visibility.Visible;
            PluginTitle.Text = pair.Metadata.Name;
            PluginTitle.Cursor = Cursors.Hand;
            PluginActionKeywords.Text = string.Join(Query.ActionKeywordSeperater, actionKeywords.ToArray());
            PluginAuthor.Text = InternationalizationManager.Instance.GetTranslation("author") + ": " +
                                pair.Metadata.Author;
            PluginSubTitle.Text = pair.Metadata.Description;
            pluginId = pair.Metadata.ID;
            PluginIcon.Source = ImageLoader.Load(pair.Metadata.IcoPath);

            var customizedPluginConfig = _settings.PluginSettings.Plugins[pluginId];
            DisablePlugin.IsChecked = customizedPluginConfig != null && customizedPluginConfig.Disabled;

            PluginContentPanel.Content = null;
            var settingProvider = pair.Plugin as ISettingProvider;
            if (settingProvider != null)
            {
                Control control;
                if (!featureControls.TryGetValue(settingProvider, out control))
                {
                    var multipleActionKeywordsProvider = settingProvider as IMultipleActionKeywords;
                    if (multipleActionKeywordsProvider != null)
                    {
                        multipleActionKeywordsProvider.ActionKeywordsChanged += (o, e) =>
                        {
                            // update in-memory data
                            PluginManager.UpdateActionKeywordForPlugin(pair, e.OldActionKeyword, e.NewActionKeyword);
                            // update persistant data
                            _settings.PluginSettings.UpdateActionKeyword(pair.Metadata);

                            MessageBox.Show(InternationalizationManager.Instance.GetTranslation("succeed"));
                        };
                    }

                    featureControls.Add(settingProvider, control = settingProvider.CreateSettingPanel());
                }
                PluginContentPanel.Content = control;
                control.HorizontalAlignment = HorizontalAlignment.Stretch;
                control.VerticalAlignment = VerticalAlignment.Stretch;
                control.Width = Double.NaN;
                control.Height = Double.NaN;
            }
        }

        private void OnDisablePluginClicked(object sender, RoutedEventArgs e)
        {
            var checkBox = (CheckBox)e.Source;
            var pair = (PluginPair)PluginsListBox.SelectedItem;
            var id = pair.Metadata.ID;
            if (checkBox.IsChecked != null)
            {
                var disabled = (bool)checkBox.IsChecked;
                _settings.PluginSettings.Plugins[id].Disabled = disabled;
            }
            else
            {
                Log.Warn($"IsChecked for checkbox is null for plugin: {pair.Metadata.Name}");
            }
        }

        private void PluginActionKeywords_OnMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                var pair = PluginsListBox.SelectedItem as PluginPair;
                if (pair != null)
                {
                    //third-party plugin
                    string id = pair.Metadata.ID;
                    ActionKeywords changeKeywordsWindow = new ActionKeywords(id, _settings);
                    changeKeywordsWindow.ShowDialog();
                    PluginPair plugin = PluginManager.GetPluginForId(id);
                    if (plugin != null)
                        PluginActionKeywords.Text = string.Join(Query.ActionKeywordSeperater,
                            pair.Metadata.ActionKeywords.ToArray());
                }
            }
        }

        private void PluginTitle_OnMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                var pair = PluginsListBox.SelectedItem as PluginPair;
                if (pair != null)
                {
                    //third-party plugin
                    if (!string.IsNullOrEmpty(pair.Metadata.Website))
                    {
                        try
                        {
                            Process.Start(pair.Metadata.Website);
                        }
                        catch
                        {
                        }
                    }
                }
            }
        }

        private void tbOpenPluginDirecoty_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                var pair = PluginsListBox.SelectedItem as PluginPair;
                if (pair != null)
                {
                    //third-party plugin
                    if (!string.IsNullOrEmpty(pair.Metadata.Website))
                    {
                        try
                        {
                            Process.Start(pair.Metadata.PluginDirectory);
                        }
                        catch
                        {
                        }
                    }
                }
            }
        }

        private void tbMorePlugins_MouseUp(object sender, MouseButtonEventArgs e)
        {
            Process.Start("http://www.getwox.com/plugin");
        }

        private void OnPluginTabSelected()
        {
            var plugins = PluginManager.AllPlugins;
            //move all disabled to bottom
            plugins.Sort(delegate (PluginPair a, PluginPair b)
            {
                int res = _settings.PluginSettings.Plugins[a.Metadata.ID].Disabled ? 1 : 0;
                res += _settings.PluginSettings.Plugins[b.Metadata.ID].Disabled ? -1 : 0;
                return res;
            });

            PluginsListBox.ItemsSource = new CompositeCollection
            {
                new CollectionContainer
                {
                    Collection = plugins
                }
            }; ;
            PluginsListBox.SelectedIndex = 0;
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
