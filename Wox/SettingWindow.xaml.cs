using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Wox.Core.Plugin;
using Wox.Plugin;
using Wox.Helper;
using Application = System.Windows.Forms.Application;
using File = System.IO.File;
using MessageBox = System.Windows.MessageBox;
using System.Windows.Data;
using System.Windows.Forms;
using Microsoft.Win32;
using Wox.Core.i18n;
using Wox.Core.Theme;
using Wox.Core.Updater;
using Wox.Core.UserSettings;
using Wox.Infrastructure;
using CheckBox = System.Windows.Controls.CheckBox;
using Control = System.Windows.Controls.Control;
using Cursors = System.Windows.Input.Cursors;
using HorizontalAlignment = System.Windows.HorizontalAlignment;

namespace Wox
{
    public partial class SettingWindow : Window
    {
        public readonly MainWindow MainWindow;
        bool settingsLoaded = false;
        private Dictionary<ISettingProvider, Control> featureControls = new Dictionary<ISettingProvider, Control>();
        private bool themeTabLoaded = false;

        public SettingWindow(MainWindow mainWindow)
        {
            this.MainWindow = mainWindow;
            InitializeComponent();
            Loaded += Setting_Loaded;
        }

        private void Setting_Loaded(object sender, RoutedEventArgs ev)
        {
            #region General

            cbHideWhenDeactive.Checked += (o, e) =>
            {
                UserSettingStorage.Instance.HideWhenDeactive = true;
                UserSettingStorage.Instance.Save();
            };

            cbHideWhenDeactive.Unchecked += (o, e) =>
            {
                UserSettingStorage.Instance.HideWhenDeactive = false;
                UserSettingStorage.Instance.Save();
            };

            cbRememberLastLocation.Checked += (o, e) =>
            {
                UserSettingStorage.Instance.RememberLastLaunchLocation = true;
                UserSettingStorage.Instance.Save();
            };

            cbRememberLastLocation.Unchecked += (o, e) =>
            {
                UserSettingStorage.Instance.RememberLastLaunchLocation = false;
                UserSettingStorage.Instance.Save();
            };

            cbDontPromptUpdateMsg.Checked += (o, e) =>
            {
                UserSettingStorage.Instance.DontPromptUpdateMsg = true;
                UserSettingStorage.Instance.Save();
            };

            cbDontPromptUpdateMsg.Unchecked += (o, e) =>
            {
                UserSettingStorage.Instance.DontPromptUpdateMsg = false;
                UserSettingStorage.Instance.Save();
            };

            cbStartWithWindows.IsChecked = CheckApplicationIsStartupWithWindow();
            comboMaxResultsToShow.SelectionChanged += (o, e) =>
            {
                UserSettingStorage.Instance.MaxResultsToShow = (int)comboMaxResultsToShow.SelectedItem;
                UserSettingStorage.Instance.Save();
                MainWindow.pnlResult.lbResults.GetBindingExpression(MaxHeightProperty).UpdateTarget();
            };

            cbHideWhenDeactive.IsChecked = UserSettingStorage.Instance.HideWhenDeactive;
            cbDontPromptUpdateMsg.IsChecked = UserSettingStorage.Instance.DontPromptUpdateMsg;
            cbRememberLastLocation.IsChecked = UserSettingStorage.Instance.RememberLastLaunchLocation;

            LoadLanguages();
            comboMaxResultsToShow.ItemsSource = Enumerable.Range(2, 16);
            var maxResults = UserSettingStorage.Instance.MaxResultsToShow;
            comboMaxResultsToShow.SelectedItem = maxResults == 0 ? 6 : maxResults;

            #endregion

            #region Proxy

            cbEnableProxy.Checked += (o, e) => EnableProxy();
            cbEnableProxy.Unchecked += (o, e) => DisableProxy();
            cbEnableProxy.IsChecked = UserSettingStorage.Instance.ProxyEnabled;
            tbProxyServer.Text = UserSettingStorage.Instance.ProxyServer;
            if (UserSettingStorage.Instance.ProxyPort != 0) {
                tbProxyPort.Text = UserSettingStorage.Instance.ProxyPort.ToString();
            }
            tbProxyUserName.Text = UserSettingStorage.Instance.ProxyUserName;
            tbProxyPassword.Password = UserSettingStorage.Instance.ProxyPassword;
            if (UserSettingStorage.Instance.ProxyEnabled)
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
                UserSettingStorage.Instance.ActivateTimes);
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
            cbLanguages.SelectedValue = UserSettingStorage.Instance.Language;
            cbLanguages.SelectionChanged += cbLanguages_SelectionChanged;
        }

        void cbLanguages_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            InternationalizationManager.Instance.ChangeLanguage(cbLanguages.SelectedItem as Language);
        }

        private void CbStartWithWindows_OnChecked(object sender, RoutedEventArgs e)
        {
            AddApplicationToStartup();
            UserSettingStorage.Instance.StartWoxOnSystemStartup = true;
            UserSettingStorage.Instance.Save();
        }

        private void CbStartWithWindows_OnUnchecked(object sender, RoutedEventArgs e)
        {
            RemoveApplicationFromStartup();
            UserSettingStorage.Instance.StartWoxOnSystemStartup = false;
            UserSettingStorage.Instance.Save();
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

        void ctlHotkey_OnHotkeyChanged(object sender, System.EventArgs e)
        {
            if (ctlHotkey.CurrentHotkeyAvailable)
            {
                MainWindow.SetHotkey(ctlHotkey.CurrentHotkey, delegate
                {
                    if (!MainWindow.IsVisible)
                    {
                        MainWindow.ShowApp();
                    }
                    else
                    {
                        MainWindow.HideApp();
                    }
                });
                MainWindow.RemoveHotkey(UserSettingStorage.Instance.Hotkey);
                UserSettingStorage.Instance.Hotkey = ctlHotkey.CurrentHotkey.ToString();
                UserSettingStorage.Instance.Save();
            }
        }

        private void OnHotkeyTabSelected()
        {
            ctlHotkey.HotkeyChanged += ctlHotkey_OnHotkeyChanged;
            ctlHotkey.SetHotkey(UserSettingStorage.Instance.Hotkey, false);
            lvCustomHotkey.ItemsSource = UserSettingStorage.Instance.CustomPluginHotkeys;
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
                UserSettingStorage.Instance.CustomPluginHotkeys.Remove(item);
                lvCustomHotkey.Items.Refresh();
                UserSettingStorage.Instance.Save();
                MainWindow.RemoveHotkey(item.Hotkey);
            }
        }

        private void BtnEditCustomHotkey_OnClick(object sender, RoutedEventArgs e)
        {
            CustomPluginHotkey item = lvCustomHotkey.SelectedItem as CustomPluginHotkey;
            if (item != null)
            {
                CustomQueryHotkeySetting window = new CustomQueryHotkeySetting(this);
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
            new CustomQueryHotkeySetting(this).ShowDialog();
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
            using (new Timeit("theme load"))
            {
                var s = Fonts.SystemFontFamilies;
            }

            if (themeTabLoaded) return;

            themeTabLoaded = true;
            if (!string.IsNullOrEmpty(UserSettingStorage.Instance.QueryBoxFont) &&
                Fonts.SystemFontFamilies.Count(o => o.FamilyNames.Values.Contains(UserSettingStorage.Instance.QueryBoxFont)) > 0)
            {
                cbQueryBoxFont.Text = UserSettingStorage.Instance.QueryBoxFont;

                cbQueryBoxFontFaces.SelectedItem = SyntaxSugars.CallOrRescueDefault(() => ((FontFamily)cbQueryBoxFont.SelectedItem).ConvertFromInvariantStringsOrNormal(
                    UserSettingStorage.Instance.QueryBoxFontStyle,
                    UserSettingStorage.Instance.QueryBoxFontWeight,
                    UserSettingStorage.Instance.QueryBoxFontStretch
                    ));
            }
            if (!string.IsNullOrEmpty(UserSettingStorage.Instance.ResultItemFont) &&
                Fonts.SystemFontFamilies.Count(o => o.FamilyNames.Values.Contains(UserSettingStorage.Instance.ResultItemFont)) > 0)
            {
                cbResultItemFont.Text = UserSettingStorage.Instance.ResultItemFont;

                cbResultItemFontFaces.SelectedItem = SyntaxSugars.CallOrRescueDefault(() => ((FontFamily)cbResultItemFont.SelectedItem).ConvertFromInvariantStringsOrNormal(
                    UserSettingStorage.Instance.ResultItemFontStyle,
                    UserSettingStorage.Instance.ResultItemFontWeight,
                    UserSettingStorage.Instance.ResultItemFontStretch
                    ));
            }

            resultPanelPreview.AddResults(new List<Result>()
            {
                new Result()
                {
                    Title = "Wox is an effective launcher for windows",
                    SubTitle = "Wox provide bundles of features let you access infomations quickly.",
                    IcoPath = "Images/app.png",
                    PluginDirectory = Path.GetDirectoryName(System.Windows.Forms.Application.ExecutablePath)
                },
                new Result()
                {
                    Title = "Search applications",
                    SubTitle = "Search applications, files (via everything plugin) and browser bookmarks",
                    IcoPath = "Images/app.png",
                    PluginDirectory = Path.GetDirectoryName(System.Windows.Forms.Application.ExecutablePath)
                },
                new Result()
                {
                    Title = "Search web contents with shortcuts",
                    SubTitle = "e.g. search google with g keyword or youtube keyword)",
                    IcoPath = "Images/app.png",
                    PluginDirectory = Path.GetDirectoryName(Application.ExecutablePath)
                },
                new Result()
                {
                    Title = "clipboard history ",
                    IcoPath = "Images/app.png",
                    PluginDirectory = Path.GetDirectoryName(Application.ExecutablePath)
                },
                new Result()
                {
                    Title = "Themes support",
                    SubTitle = "get more themes from http://www.getwox.com/theme",
                    IcoPath = "Images/app.png",
                    PluginDirectory = Path.GetDirectoryName(Application.ExecutablePath)
                },
                new Result()
                {
                    Title = "Plugins support",
                    SubTitle = "get more plugins from http://www.getwox.com/plugin",
                    IcoPath = "Images/app.png",
                    PluginDirectory = Path.GetDirectoryName(Application.ExecutablePath)
                },
                new Result()
                {
                    Title = "Wox is an open-source software",
                    SubTitle = "Wox benefits from the open-source community a lot",
                    IcoPath = "Images/app.png",
                    PluginDirectory = Path.GetDirectoryName(Application.ExecutablePath)
                }
            });

            foreach (string theme in ThemeManager.Theme.LoadAvailableThemes())
            {
                string themeName = System.IO.Path.GetFileNameWithoutExtension(theme);
                themeComboBox.Items.Add(themeName);
            }

            themeComboBox.SelectedItem = UserSettingStorage.Instance.Theme;

            var wallpaper = WallpaperPathRetrieval.GetWallpaperPath();
            if (wallpaper != null && File.Exists(wallpaper))
            {
                var brush = new ImageBrush(new BitmapImage(new Uri(wallpaper)));
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
            UserSettingStorage.Instance.Theme = themeName;
            DelayChangeTheme();
            UserSettingStorage.Instance.Save();
        }

        private void CbQueryBoxFont_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!settingsLoaded) return;
            string queryBoxFontName = cbQueryBoxFont.SelectedItem.ToString();
            UserSettingStorage.Instance.QueryBoxFont = queryBoxFontName;
            this.cbQueryBoxFontFaces.SelectedItem = ((FontFamily)cbQueryBoxFont.SelectedItem).ChooseRegularFamilyTypeface();
            DelayChangeTheme();
            UserSettingStorage.Instance.Save();
        }

        private void DelayChangeTheme()
        {
            Dispatcher.DelayInvoke("delayChangeTheme", o =>
            {
                ThemeManager.Theme.ChangeTheme(UserSettingStorage.Instance.Theme);
            }, TimeSpan.FromMilliseconds(100));
        }

        private void CbQueryBoxFontFaces_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!settingsLoaded) return;
            FamilyTypeface typeface = (FamilyTypeface)cbQueryBoxFontFaces.SelectedItem;
            if (typeface == null)
            {
                if (cbQueryBoxFontFaces.Items.Count > 0)
                    cbQueryBoxFontFaces.SelectedIndex = 0;

                return;
            }
            else
            {
                UserSettingStorage.Instance.QueryBoxFontStretch = typeface.Stretch.ToString();
                UserSettingStorage.Instance.QueryBoxFontWeight = typeface.Weight.ToString();
                UserSettingStorage.Instance.QueryBoxFontStyle = typeface.Style.ToString();
                UserSettingStorage.Instance.Save();
                DelayChangeTheme();
            }
        }

        private void CbResultItemFont_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!settingsLoaded) return;
            string resultItemFont = cbResultItemFont.SelectedItem.ToString();
            UserSettingStorage.Instance.ResultItemFont = resultItemFont;
            this.cbResultItemFontFaces.SelectedItem = ((FontFamily)cbResultItemFont.SelectedItem).ChooseRegularFamilyTypeface();

            UserSettingStorage.Instance.Save();
            DelayChangeTheme();
        }

        private void CbResultItemFontFaces_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!settingsLoaded) return;
            FamilyTypeface typeface = (FamilyTypeface)cbResultItemFontFaces.SelectedItem;
            if (typeface == null)
            {
                if (cbResultItemFontFaces.Items.Count > 0)
                    cbResultItemFontFaces.SelectedIndex = 0;
            }
            else
            {
                UserSettingStorage.Instance.ResultItemFontStretch = typeface.Stretch.ToString();
                UserSettingStorage.Instance.ResultItemFontWeight = typeface.Weight.ToString();
                UserSettingStorage.Instance.ResultItemFontStyle = typeface.Style.ToString();
                UserSettingStorage.Instance.Save();
                DelayChangeTheme();
            }
        }

        #endregion

        #region Plugin

        private void lbPlugins_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ISettingProvider provider = null;
            var pair = lbPlugins.SelectedItem as PluginPair;
            string pluginId = string.Empty;

            if (pair != null)
            {
                provider = pair.Plugin as ISettingProvider;
                pluginAuthor.Visibility = Visibility.Visible;
                pluginActionKeyword.Visibility = Visibility.Visible;
                pluginInitTime.Text =
                    string.Format(InternationalizationManager.Instance.GetTranslation("plugin_init_time"), pair.InitTime);
                pluginQueryTime.Text =
                    string.Format(InternationalizationManager.Instance.GetTranslation("plugin_query_time"), pair.AvgQueryTime);
                pluginActionKeywordTitle.Visibility = Visibility.Visible;
                tbOpenPluginDirecoty.Visibility = Visibility.Visible;
                pluginTitle.Text = pair.Metadata.Name;
                pluginTitle.Cursor = Cursors.Hand;
                pluginActionKeyword.Text = pair.Metadata.ActionKeyword;
                pluginAuthor.Text = InternationalizationManager.Instance.GetTranslation("author") + ": " + pair.Metadata.Author;
                pluginSubTitle.Text = pair.Metadata.Description;
                pluginId = pair.Metadata.ID;
                pluginIcon.Source = ImageLoader.ImageLoader.Load(pair.Metadata.FullIcoPath);
            }

            var customizedPluginConfig = UserSettingStorage.Instance.CustomizedPluginConfigs.FirstOrDefault(o => o.ID == pluginId);
            cbDisablePlugin.IsChecked = customizedPluginConfig != null && customizedPluginConfig.Disabled;

            PluginContentPanel.Content = null;
            if (provider != null)
            {
                Control control = null;
                if (!featureControls.TryGetValue(provider, out control))
                    featureControls.Add(provider, control = provider.CreateSettingPanel());

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
            var id = string.Empty;
            var name = string.Empty;
            if (pair != null)
            {
                //third-party plugin
                id = pair.Metadata.ID;
                name = pair.Metadata.Name;
            }
            var customizedPluginConfig = UserSettingStorage.Instance.CustomizedPluginConfigs.FirstOrDefault(o => o.ID == id);
            if (customizedPluginConfig == null)
            {
                UserSettingStorage.Instance.CustomizedPluginConfigs.Add(new CustomizedPluginConfig()
                {
                    Disabled = cbDisabled.IsChecked ?? true,
                    ID = id,
                    Name = name,
                    Actionword = string.Empty
                });
            }
            else
            {
                customizedPluginConfig.Disabled = cbDisabled.IsChecked ?? true;
            }
            UserSettingStorage.Instance.Save();
        }

        private void PluginActionKeyword_OnMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                var pair = lbPlugins.SelectedItem as PluginPair;
                if (pair != null)
                {
                    //third-party plugin
                    string id = pair.Metadata.ID;
                    ActionKeyword changeKeywordWindow = new ActionKeyword(id);
                    changeKeywordWindow.ShowDialog();
                    PluginPair plugin = PluginManager.GetPlugin(id);
                    if (plugin != null) pluginActionKeyword.Text = plugin.Metadata.ActionKeyword;
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
            UserSettingStorage.Instance.ProxyEnabled = cbEnableProxy.IsChecked ?? false;

            int port = 80;
            if (UserSettingStorage.Instance.ProxyEnabled)
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

            UserSettingStorage.Instance.ProxyServer = tbProxyServer.Text;
            UserSettingStorage.Instance.ProxyPort = port;
            UserSettingStorage.Instance.ProxyUserName = tbProxyUserName.Text;
            UserSettingStorage.Instance.ProxyPassword = tbProxyPassword.Password;
            UserSettingStorage.Instance.Save();

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

        private void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            // Hide window with ESC, but make sure it is not pressed as a hotkey
            if (e.Key == Key.Escape && !ctlHotkey.IsFocused)
            {
                Close();
            }
        }
    }
}
