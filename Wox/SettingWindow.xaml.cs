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
using Microsoft.Win32;
using NHotkey;
using NHotkey.Wpf;
using Wox.Core.Plugin;
using Wox.Core.Resource;
using Wox.Core.Updater;
using Wox.Core.UserSettings;
using Wox.Helper;
using Wox.Infrastructure.Hotkey;
using Wox.Infrastructure.Image;
using Wox.Plugin;
using Wox.ViewModel;
using Application = System.Windows.Forms.Application;
using Stopwatch = Wox.Infrastructure.Stopwatch;

namespace Wox
{
    public partial class SettingWindow : Window
    {
        public readonly IPublicAPI _api;
        bool settingsLoaded;
        private Dictionary<ISettingProvider, Control> featureControls = new Dictionary<ISettingProvider, Control>();
        private bool themeTabLoaded;
        private Settings _settings;

        public SettingWindow(IPublicAPI api, Settings settings)
        {
            InitializeComponent();
            _settings = settings;
            _api = api;
            ResultListBoxPreview.DataContext = new ResultsViewModel(_settings, null);
            Loaded += Setting_Loaded;
        }

        private void Setting_Loaded(object sender, RoutedEventArgs ev)
        {
            #region General
            cbHideWhenDeactive.Checked += (o, e) =>
            {
                _settings.HideWhenDeactive = true;
            };

            cbHideWhenDeactive.Unchecked += (o, e) =>
            {
                _settings.HideWhenDeactive = false;
            };

            cbRememberLastLocation.Checked += (o, e) =>
            {
                _settings.RememberLastLaunchLocation = true;
            };

            cbRememberLastLocation.Unchecked += (o, e) =>
            {
                _settings.RememberLastLaunchLocation = false;
            };

            cbDontPromptUpdateMsg.Checked += (o, e) =>
            {
                _settings.DontPromptUpdateMsg = true;
            };

            cbDontPromptUpdateMsg.Unchecked += (o, e) =>
            {
                _settings.DontPromptUpdateMsg = false;
            };

            cbIgnoreHotkeysOnFullscreen.Checked += (o, e) =>
            {
                _settings.IgnoreHotkeysOnFullscreen = true;
            };


            cbIgnoreHotkeysOnFullscreen.Unchecked += (o, e) =>
            {
                _settings.IgnoreHotkeysOnFullscreen = false;
            };


            cbStartWithWindows.IsChecked = CheckApplicationIsStartupWithWindow();
            comboMaxResultsToShow.SelectionChanged += (o, e) =>
            {
                _settings.MaxResultsToShow = (int)comboMaxResultsToShow.SelectedItem;
                //MainWindow.pnlResult.lbResults.GetBindingExpression(MaxHeightProperty).UpdateTarget();
            };

            cbHideWhenDeactive.IsChecked = _settings.HideWhenDeactive;
            cbDontPromptUpdateMsg.IsChecked = _settings.DontPromptUpdateMsg;
            cbRememberLastLocation.IsChecked = _settings.RememberLastLaunchLocation;
            cbIgnoreHotkeysOnFullscreen.IsChecked = _settings.IgnoreHotkeysOnFullscreen;

            LoadLanguages();
            comboMaxResultsToShow.ItemsSource = Enumerable.Range(2, 16);
            var maxResults = _settings.MaxResultsToShow;
            comboMaxResultsToShow.SelectedItem = maxResults == 0 ? 6 : maxResults;

            #endregion

            #region Proxy

            cbEnableProxy.Checked += (o, e) => EnableProxy();
            cbEnableProxy.Unchecked += (o, e) => DisableProxy();
            cbEnableProxy.IsChecked = _settings.ProxyEnabled;
            tbProxyServer.Text = _settings.ProxyServer;
            if (_settings.ProxyPort != 0)
            {
                tbProxyPort.Text = _settings.ProxyPort.ToString();
            }
            tbProxyUserName.Text = _settings.ProxyUserName;
            tbProxyPassword.Password = _settings.ProxyPassword;
            if (_settings.ProxyEnabled)
            {
                EnableProxy();
            }
            else
            {
                DisableProxy();
            }

            #endregion

            #region About

            tbVersion.Text = UpdaterManager.Instance.CurrentVersion.ToString();
            string activateTimes = string.Format(InternationalizationManager.Instance.GetTranslation("about_activate_times"),
                _settings.ActivateTimes);
            tbActivatedTimes.Text = activateTimes;

            #endregion

            settingsLoaded = true;
        }

        public void SwitchTo(string tabName)
        {
            switch (tabName)
            {
                case "general":
                    settingTab.SelectedIndex = 0;
                    break;
                case "plugin":
                    settingTab.SelectedIndex = 1;
                    break;
                case "theme":
                    settingTab.SelectedIndex = 2;
                    break;
                case "hotkey":
                    settingTab.SelectedIndex = 3;
                    break;
                case "proxy":
                    settingTab.SelectedIndex = 4;
                    break;
                case "about":
                    settingTab.SelectedIndex = 5;
                    break;
            }
        }

        private void settingTab_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Update controls inside the selected tab
            if (e.OriginalSource != settingTab) return;

            if (tabPlugin.IsSelected)
            {
                OnPluginTabSelected();
            }
            else if (tabTheme.IsSelected)
            {
                OnThemeTabSelected();
            }
            else if (tabHotkey.IsSelected)
            {
                OnHotkeyTabSelected();
            }
        }

        #region General

        private void LoadLanguages()
        {
            cbLanguages.ItemsSource = InternationalizationManager.Instance.LoadAvailableLanguages();
            cbLanguages.DisplayMemberPath = "Display";
            cbLanguages.SelectedValuePath = "LanguageCode";
            cbLanguages.SelectedValue = _settings.Language;
            cbLanguages.SelectionChanged += cbLanguages_SelectionChanged;
        }

        void cbLanguages_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            InternationalizationManager.Instance.ChangeLanguage(cbLanguages.SelectedItem as Language);
        }

        private void CbStartWithWindows_OnChecked(object sender, RoutedEventArgs e)
        {
            AddApplicationToStartup();
            _settings.StartWoxOnSystemStartup = true;
        }

        private void CbStartWithWindows_OnUnchecked(object sender, RoutedEventArgs e)
        {
            RemoveApplicationFromStartup();
            _settings.StartWoxOnSystemStartup = false;
        }

        private void AddApplicationToStartup()
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true))
            {
                key.SetValue("Wox", "\"" + Application.ExecutablePath + "\" --hidestart");
            }
        }

        private void RemoveApplicationFromStartup()
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true))
            {
                key.DeleteValue("Wox", false);
            }
        }

        private bool CheckApplicationIsStartupWithWindow()
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true))
            {
                return key.GetValue("Wox") != null;
            }
        }

        #endregion

        #region Hotkey

        void ctlHotkey_OnHotkeyChanged(object sender, EventArgs e)
        {
            if (ctlHotkey.CurrentHotkeyAvailable)
            {
                SetHotkey(ctlHotkey.CurrentHotkey, delegate
                {
                    if (!App.Window.IsVisible)
                    {
                        _api.ShowApp();
                    }
                    else
                    {
                        _api.HideApp();
                    }
                });
                RemoveHotkey(_settings.Hotkey);
                _settings.Hotkey = ctlHotkey.CurrentHotkey.ToString();
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
                string errorMsg = string.Format(InternationalizationManager.Instance.GetTranslation("registerHotkeyFailed"), hotkeyStr);
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
            ctlHotkey.HotkeyChanged += ctlHotkey_OnHotkeyChanged;
            ctlHotkey.SetHotkey(_settings.Hotkey, false);
            lvCustomHotkey.ItemsSource = _settings.CustomPluginHotkeys;
        }

        private void BtnDeleteCustomHotkey_OnClick(object sender, RoutedEventArgs e)
        {
            CustomPluginHotkey item = lvCustomHotkey.SelectedItem as CustomPluginHotkey;
            if (item == null)
            {
                MessageBox.Show(InternationalizationManager.Instance.GetTranslation("pleaseSelectAnItem"));
                return;
            }

            string deleteWarning = string.Format(InternationalizationManager.Instance.GetTranslation("deleteCustomHotkeyWarning"), item.Hotkey);
            if (MessageBox.Show(deleteWarning, InternationalizationManager.Instance.GetTranslation("delete"), MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                _settings.CustomPluginHotkeys.Remove(item);
                lvCustomHotkey.Items.Refresh();
                RemoveHotkey(item.Hotkey);
            }
        }

        private void BtnEditCustomHotkey_OnClick(object sender, RoutedEventArgs e)
        {
            CustomPluginHotkey item = lvCustomHotkey.SelectedItem as CustomPluginHotkey;
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
            lvCustomHotkey.Items.Refresh();
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
                cbQueryBoxFont.Text = _settings.QueryBoxFont;

                cbQueryBoxFontFaces.SelectedItem = SyntaxSugars.CallOrRescueDefault(() => ((FontFamily)cbQueryBoxFont.SelectedItem).ConvertFromInvariantStringsOrNormal(
                    _settings.QueryBoxFontStyle,
                    _settings.QueryBoxFontWeight,
                    _settings.QueryBoxFontStretch
                    ));
            }
            if (!string.IsNullOrEmpty(_settings.ResultFont) &&
                Fonts.SystemFontFamilies.Count(o => o.FamilyNames.Values.Contains(_settings.ResultFont)) > 0)
            {
                ResultFontComboBox.Text = _settings.ResultFont;

                ResultFontFacesComboBox.SelectedItem = SyntaxSugars.CallOrRescueDefault(() => ((FontFamily)ResultFontComboBox.SelectedItem).ConvertFromInvariantStringsOrNormal(
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
                    PluginDirectory = Path.GetDirectoryName(Application.ExecutablePath)
                },
                new Result
                {
                    Title = "Search applications",
                    SubTitle = "Search applications, files (via everything plugin) and browser bookmarks",
                    IcoPath = "Images/app.png",
                    PluginDirectory = Path.GetDirectoryName(Application.ExecutablePath)
                },
                new Result
                {
                    Title = "Search web contents with shortcuts",
                    SubTitle = "e.g. search google with g keyword or youtube keyword)",
                    IcoPath = "Images/app.png",
                    PluginDirectory = Path.GetDirectoryName(Application.ExecutablePath)
                },
                new Result
                {
                    Title = "clipboard history ",
                    IcoPath = "Images/app.png",
                    PluginDirectory = Path.GetDirectoryName(Application.ExecutablePath)
                },
                new Result
                {
                    Title = "Themes support",
                    SubTitle = "get more themes from http://www.getwox.com/theme",
                    IcoPath = "Images/app.png",
                    PluginDirectory = Path.GetDirectoryName(Application.ExecutablePath)
                },
                new Result
                {
                    Title = "Plugins support",
                    SubTitle = "get more plugins from http://www.getwox.com/plugin",
                    IcoPath = "Images/app.png",
                    PluginDirectory = Path.GetDirectoryName(Application.ExecutablePath)
                },
                new Result
                {
                    Title = "Wox is an open-source software",
                    SubTitle = "Wox benefits from the open-source community a lot",
                    IcoPath = "Images/app.png",
                    PluginDirectory = Path.GetDirectoryName(Application.ExecutablePath)
                }
            });

            foreach (string theme in ThemeManager.Instance.LoadAvailableThemes())
            {
                string themeName = Path.GetFileNameWithoutExtension(theme);
                themeComboBox.Items.Add(themeName);
            }

            themeComboBox.SelectedItem = _settings.Theme;

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
            string themeName = themeComboBox.SelectedItem.ToString();
            ThemeManager.Instance.ChangeTheme(themeName);
        }

        private void CbQueryBoxFont_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!settingsLoaded) return;
            string queryBoxFontName = cbQueryBoxFont.SelectedItem.ToString();
            _settings.QueryBoxFont = queryBoxFontName;
            cbQueryBoxFontFaces.SelectedItem = ((FontFamily)cbQueryBoxFont.SelectedItem).ChooseRegularFamilyTypeface();
            ThemeManager.Instance.ChangeTheme(_settings.Theme);
        }

        private void CbQueryBoxFontFaces_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!settingsLoaded) return;
            FamilyTypeface typeface = (FamilyTypeface)cbQueryBoxFontFaces.SelectedItem;
            if (typeface == null)
            {
                if (cbQueryBoxFontFaces.Items.Count > 0)
                    cbQueryBoxFontFaces.SelectedIndex = 0;
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
            ResultFontFacesComboBox.SelectedItem = ((FontFamily)ResultFontComboBox.SelectedItem).ChooseRegularFamilyTypeface();
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

            var pair = lbPlugins.SelectedItem as PluginPair;
            string pluginId = string.Empty;
            List<string> actionKeywords = null;
            if (pair == null) return;
            actionKeywords = pair.Metadata.ActionKeywords;
            pluginAuthor.Visibility = Visibility.Visible;
            pluginInitTime.Text =
                string.Format(InternationalizationManager.Instance.GetTranslation("plugin_init_time"), pair.InitTime);
            pluginQueryTime.Text =
                string.Format(InternationalizationManager.Instance.GetTranslation("plugin_query_time"), pair.AvgQueryTime);
            if (actionKeywords.Count > 1)
            {
                pluginActionKeywordsTitle.Visibility = Visibility.Collapsed;
                pluginActionKeywords.Visibility = Visibility.Collapsed;
            }
            else
            {
                pluginActionKeywordsTitle.Visibility = Visibility.Visible;
                pluginActionKeywords.Visibility = Visibility.Visible;
            }
            tbOpenPluginDirecoty.Visibility = Visibility.Visible;
            pluginTitle.Text = pair.Metadata.Name;
            pluginTitle.Cursor = Cursors.Hand;
            pluginActionKeywords.Text = string.Join(Query.ActionKeywordSeperater, actionKeywords.ToArray());
            pluginAuthor.Text = InternationalizationManager.Instance.GetTranslation("author") + ": " + pair.Metadata.Author;
            pluginSubTitle.Text = pair.Metadata.Description;
            pluginId = pair.Metadata.ID;
            pluginIcon.Source = ImageLoader.Load(pair.Metadata.FullIcoPath);

            var customizedPluginConfig = _settings.PluginSettings[pluginId];
            cbDisablePlugin.IsChecked = customizedPluginConfig != null && customizedPluginConfig.Disabled;

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
                            _settings.UpdateActionKeyword(pair.Metadata);

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

        private void CbDisablePlugin_OnClick(object sender, RoutedEventArgs e)
        {
            CheckBox cbDisabled = e.Source as CheckBox;
            if (cbDisabled == null) return;

            var pair = lbPlugins.SelectedItem as PluginPair;
            if (pair != null)
            {
                var id = pair.Metadata.ID;
                var customizedPluginConfig = _settings.PluginSettings[id];
                if (customizedPluginConfig.Disabled)
                {
                    PluginManager.DisablePlugin(pair);
                }
                else
                {
                    PluginManager.EnablePlugin(pair);
                }
            }
        }

        private void PluginActionKeywords_OnMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                var pair = lbPlugins.SelectedItem as PluginPair;
                if (pair != null)
                {
                    //third-party plugin
                    string id = pair.Metadata.ID;
                    ActionKeywords changeKeywordsWindow = new ActionKeywords(id, _settings);
                    changeKeywordsWindow.ShowDialog();
                    PluginPair plugin = PluginManager.GetPluginForId(id);
                    if (plugin != null) pluginActionKeywords.Text = string.Join(Query.ActionKeywordSeperater, pair.Metadata.ActionKeywords.ToArray());
                }
            }
        }

        private void PluginTitle_OnMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                var pair = lbPlugins.SelectedItem as PluginPair;
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
                        { }
                    }
                }
            }
        }

        private void tbOpenPluginDirecoty_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                var pair = lbPlugins.SelectedItem as PluginPair;
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
                        { }
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
            var plugins = new CompositeCollection
            {
                new CollectionContainer
                {
                    Collection = PluginManager.AllPlugins
                }
            };
            lbPlugins.ItemsSource = plugins;
            lbPlugins.SelectedIndex = 0;
        }

        #endregion

        #region Proxy
        private void btnSaveProxy_Click(object sender, RoutedEventArgs e)
        {
            _settings.ProxyEnabled = cbEnableProxy.IsChecked ?? false;

            int port = 80;
            if (_settings.ProxyEnabled)
            {
                if (string.IsNullOrEmpty(tbProxyServer.Text))
                {
                    MessageBox.Show(InternationalizationManager.Instance.GetTranslation("serverCantBeEmpty"));
                    return;
                }
                if (string.IsNullOrEmpty(tbProxyPort.Text))
                {
                    MessageBox.Show(InternationalizationManager.Instance.GetTranslation("portCantBeEmpty"));
                    return;
                }
                if (!int.TryParse(tbProxyPort.Text, out port))
                {
                    MessageBox.Show(InternationalizationManager.Instance.GetTranslation("invalidPortFormat"));
                    return;
                }
            }

            _settings.ProxyServer = tbProxyServer.Text;
            _settings.ProxyPort = port;
            _settings.ProxyUserName = tbProxyUserName.Text;
            _settings.ProxyPassword = tbProxyPassword.Password;

            MessageBox.Show(InternationalizationManager.Instance.GetTranslation("saveProxySuccessfully"));
        }

        private void btnTestProxy_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(tbProxyServer.Text))
            {
                MessageBox.Show(InternationalizationManager.Instance.GetTranslation("serverCantBeEmpty"));
                return;
            }
            if (string.IsNullOrEmpty(tbProxyPort.Text))
            {
                MessageBox.Show(InternationalizationManager.Instance.GetTranslation("portCantBeEmpty"));
                return;
            }
            int port;
            if (!int.TryParse(tbProxyPort.Text, out port))
            {
                MessageBox.Show(InternationalizationManager.Instance.GetTranslation("invalidPortFormat"));
                return;
            }

            HttpWebRequest request = (HttpWebRequest)WebRequest.Create("http://www.baidu.com");
            request.Timeout = 1000 * 5;
            request.ReadWriteTimeout = 1000 * 5;
            if (string.IsNullOrEmpty(tbProxyUserName.Text))
            {
                request.Proxy = new WebProxy(tbProxyServer.Text, port);
            }
            else
            {
                request.Proxy = new WebProxy(tbProxyServer.Text, port);
                request.Proxy.Credentials = new NetworkCredential(tbProxyUserName.Text, tbProxyPassword.Password);
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

        private void EnableProxy()
        {
            tbProxyPassword.IsEnabled = true;
            tbProxyServer.IsEnabled = true;
            tbProxyUserName.IsEnabled = true;
            tbProxyPort.IsEnabled = true;
        }

        private void DisableProxy()
        {
            tbProxyPassword.IsEnabled = false;
            tbProxyServer.IsEnabled = false;
            tbProxyUserName.IsEnabled = false;
            tbProxyPort.IsEnabled = false;
        }

        #endregion

        #region About

        private void tbWebsite_MouseUp(object sender, MouseButtonEventArgs e)
        {
            Process.Start("http://www.getwox.com");
        }

        #endregion

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            // Hide window with ESC, but make sure it is not pressed as a hotkey
            if (e.Key == Key.Escape && !ctlHotkey.IsFocused)
            {
                Close();
            }
        }
    }
}
