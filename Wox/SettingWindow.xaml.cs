using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using IWshRuntimeLibrary;
using Microsoft.VisualBasic.ApplicationServices;
using Wox.Converters;
using Wox.Infrastructure;
using Wox.Infrastructure.Storage;
using Wox.Infrastructure.Storage.UserSettings;
using Wox.Plugin;
using Wox.Helper;
using Wox.Plugin.SystemPlugins;
using Application = System.Windows.Forms.Application;
using File = System.IO.File;
using MessageBox = System.Windows.MessageBox;
using System.Windows.Data;

namespace Wox
{
    public partial class SettingWindow : Window
    {
        string woxLinkPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Startup), "wox.lnk");
        public MainWindow MainWindow;
        private Dictionary<ISettingProvider, Control> featureControls = new Dictionary<ISettingProvider, Control>(); 

        public SettingWindow()
        {
            InitializeComponent();
        }

        public SettingWindow(MainWindow mainWindow)
        {
            this.MainWindow = mainWindow;
            InitializeComponent();
            Loaded += Setting_Loaded;
        }

        bool settingsLoaded = false;
        private void Setting_Loaded(object sender, RoutedEventArgs ev)
        {
            ctlHotkey.OnHotkeyChanged += ctlHotkey_OnHotkeyChanged;
            ctlHotkey.SetHotkey(UserSettingStorage.Instance.Hotkey, false);
            cbReplaceWinR.Checked += (o, e) =>
            {
                UserSettingStorage.Instance.ReplaceWinR = true;
                UserSettingStorage.Instance.Save();
            };
            cbReplaceWinR.Unchecked += (o, e) =>
            {
                UserSettingStorage.Instance.ReplaceWinR = false;
                UserSettingStorage.Instance.Save();
            };

            cbEnablePythonPlugins.Checked += (o, e) =>
            {
                UserSettingStorage.Instance.EnablePythonPlugins = true;
                UserSettingStorage.Instance.Save();
            };
            cbEnablePythonPlugins.Unchecked += (o, e) =>
            {
                UserSettingStorage.Instance.EnablePythonPlugins = false;
                UserSettingStorage.Instance.Save();
            };

			cbEnableBookmarkPlugin.Checked += (o, e) => {
				UserSettingStorage.Instance.EnableBookmarkPlugin = true;
				UserSettingStorage.Instance.Save();
			};
			cbEnableBookmarkPlugin.Unchecked += (o, e) => {
				UserSettingStorage.Instance.EnableBookmarkPlugin = false;
				UserSettingStorage.Instance.Save();
			};

			cbLeaveCmdOpen.Checked += (o, e) => {
				UserSettingStorage.Instance.LeaveCmdOpen = true;
				UserSettingStorage.Instance.Save();
			};

			cbLeaveCmdOpen.Unchecked += (o, e) =>
			{
				UserSettingStorage.Instance.LeaveCmdOpen = false;
				UserSettingStorage.Instance.Save();
			};

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

            #region Load Theme Data

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
                    IcoPath = "Images/work.png",
                    PluginDirectory = Path.GetDirectoryName(System.Windows.Forms.Application.ExecutablePath)
                },
                new Result()
                {
                    Title = "Search applications",
                    SubTitle = "Search applications, files (via everything plugin) and browser bookmarks",
                    IcoPath = "Images/work.png",
                    PluginDirectory = Path.GetDirectoryName(System.Windows.Forms.Application.ExecutablePath)
                },
                new Result()
                {
                    Title = "Search web contents with shortcuts",
                    SubTitle = "e.g. search google with g keyword or youtube keyword)",
                    IcoPath = "Images/work.png",
                    PluginDirectory = Path.GetDirectoryName(System.Windows.Forms.Application.ExecutablePath)
                },
                new Result()
                {
                    Title = "clipboard history ",
                    IcoPath = "Images/work.png",
                    PluginDirectory = Path.GetDirectoryName(System.Windows.Forms.Application.ExecutablePath)
                },
                new Result()
                {
                    Title = "Themes support",
                    SubTitle = "get more themes from http://www.getwox.com/theme",
                    IcoPath = "Images/work.png",
                    PluginDirectory = Path.GetDirectoryName(System.Windows.Forms.Application.ExecutablePath)
                },
                new Result()
                {
                    Title = "Plugins support",
                    SubTitle = "get more plugins from http://www.getwox.com/plugin",
                    IcoPath = "Images/work.png",
                    PluginDirectory = Path.GetDirectoryName(System.Windows.Forms.Application.ExecutablePath)
                },
                new Result()
                {
                    Title = "Wox is an open-source software",
                    SubTitle = "Wox benefits from the open-source community a lot",
                    IcoPath = "Images/work.png",
                    PluginDirectory = Path.GetDirectoryName(System.Windows.Forms.Application.ExecutablePath)
                }
            });

            #endregion


            foreach (string theme in LoadAvailableThemes())
            {
                string themeName = System.IO.Path.GetFileNameWithoutExtension(theme);
                themeComboBox.Items.Add(themeName);
            }

            themeComboBox.SelectedItem = UserSettingStorage.Instance.Theme;
            cbReplaceWinR.IsChecked = UserSettingStorage.Instance.ReplaceWinR;
            lvCustomHotkey.ItemsSource = UserSettingStorage.Instance.CustomPluginHotkeys;
            cbEnablePythonPlugins.IsChecked = UserSettingStorage.Instance.EnablePythonPlugins;
            cbStartWithWindows.IsChecked = File.Exists(woxLinkPath);
            cbEnableBookmarkPlugin.IsChecked = UserSettingStorage.Instance.EnableBookmarkPlugin;
	        cbLeaveCmdOpen.IsChecked = UserSettingStorage.Instance.LeaveCmdOpen;
            cbHideWhenDeactive.IsChecked = UserSettingStorage.Instance.HideWhenDeactive;

            var features = new CompositeCollection
            {
                new CollectionContainer()
                {
                    Collection =
                        PluginLoader.Plugins.AllPlugins.Where(o => o.Metadata.PluginType == PluginType.System)
                            .Select(o => o.Plugin)
                            .Cast<Wox.Plugin.SystemPlugins.ISystemPlugin>()
                },
                FindResource("FeatureBoxSeperator"),
                new CollectionContainer()
                {
                    Collection =
                        PluginLoader.Plugins.AllPlugins.Where(o => o.Metadata.PluginType == PluginType.ThirdParty)
                }
            };
            featureBox.ItemsSource = features;

            slOpacity.Value = UserSettingStorage.Instance.Opacity;
            CbOpacityMode.SelectedItem = UserSettingStorage.Instance.OpacityMode;

            var wallpaper = WallpaperPathRetrieval.GetWallpaperPath();
            if (wallpaper != null && File.Exists(wallpaper))
            {
                var brush = new ImageBrush(new BitmapImage(new Uri(wallpaper)));
                brush.Stretch = Stretch.UniformToFill;
                this.PreviewPanel.Background = brush;
            }
            else
            {
                var wallpaperColor = WallpaperPathRetrieval.GetWallpaperColor();
                this.PreviewPanel.Background = new SolidColorBrush(wallpaperColor);
            }

            //PreviewPanel
            settingsLoaded = true;
            App.Window.SetTheme(UserSettingStorage.Instance.Theme);
        }

        private List<string> LoadAvailableThemes()
        {
            string themePath = Path.Combine(Path.GetDirectoryName(System.Windows.Forms.Application.ExecutablePath), "Themes");
            return Directory.GetFiles(themePath).Where(filePath => filePath.EndsWith(".xaml") && !filePath.EndsWith("Default.xaml")).ToList();
        }

        private void CbStartWithWindows_OnChecked(object sender, RoutedEventArgs e)
        {
            CreateStartupFolderShortcut();
            UserSettingStorage.Instance.StartWoxOnSystemStartup = true;
            UserSettingStorage.Instance.Save();
        }

        private void CbStartWithWindows_OnUnchecked(object sender, RoutedEventArgs e)
        {
            if (File.Exists(woxLinkPath))
            {
                File.Delete(woxLinkPath);
            }

            UserSettingStorage.Instance.StartWoxOnSystemStartup = false;
            UserSettingStorage.Instance.Save();
        }

        private void CreateStartupFolderShortcut()
        {
            WshShellClass wshShell = new WshShellClass();

            IWshShortcut shortcut = (IWshShortcut)wshShell.CreateShortcut(woxLinkPath);
            shortcut.TargetPath = Application.ExecutablePath;
            shortcut.Arguments = "hideStart";
            shortcut.WorkingDirectory = Path.GetDirectoryName(Application.ExecutablePath);
            shortcut.Description = "Launch Wox";
            shortcut.IconLocation = Path.Combine(Path.GetDirectoryName(Application.ExecutablePath), "App.ico");
            shortcut.Save();
        }

        void ctlHotkey_OnHotkeyChanged(object sender, System.EventArgs e)
        {
            if (ctlHotkey.CurrentHotkeyAvailable)
            {
                MainWindow.SetHotkey(ctlHotkey.CurrentHotkey.ToString(), delegate
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

        #region Custom Plugin Hotkey

        private void BtnDeleteCustomHotkey_OnClick(object sender, RoutedEventArgs e)
        {
            CustomPluginHotkey item = lvCustomHotkey.SelectedItem as CustomPluginHotkey;
            if (item != null &&
                MessageBox.Show("Are your sure to delete " + item.Hotkey + " plugin hotkey?", "Delete Custom Plugin Hotkey",
                    MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                UserSettingStorage.Instance.CustomPluginHotkeys.Remove(item);
                lvCustomHotkey.Items.Refresh();
                UserSettingStorage.Instance.Save();
                MainWindow.RemoveHotkey(item.Hotkey);
            }
            else
            {
                MessageBox.Show("Please select an item");
            }
        }

        private void BtnEditCustomHotkey_OnClick(object sender, RoutedEventArgs e)
        {
            CustomPluginHotkey item = lvCustomHotkey.SelectedItem as CustomPluginHotkey;
            if (item != null)
            {
                CustomPluginHotkeySetting window = new CustomPluginHotkeySetting(this);
                window.UpdateItem(item);
                window.ShowDialog();
            }
            else
            {
                MessageBox.Show("Please select an item");
            }
        }

        private void BtnAddCustomeHotkey_OnClick(object sender, RoutedEventArgs e)
        {
            new CustomPluginHotkeySetting(this).ShowDialog();
        }

        public void ReloadCustomPluginHotkeyView()
        {
            lvCustomHotkey.Items.Refresh();
        }

        #endregion

        private void BtnEnableInstaller_OnClick(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start("Wox.UAC.exe", "AssociatePluginInstaller");
            }
            catch (Exception)
            {
                //throw an exception when user click cancel in UAC prompt window.
            }
        }

        #region Theme
        private void ThemeComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            string themeName = themeComboBox.SelectedItem.ToString();
            MainWindow.SetTheme(themeName);
            UserSettingStorage.Instance.Theme = themeName;
            UserSettingStorage.Instance.Save();
        }

        private void CbQueryBoxFont_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!settingsLoaded) return;
            string queryBoxFontName = cbQueryBoxFont.SelectedItem.ToString();
            UserSettingStorage.Instance.QueryBoxFont = queryBoxFontName;
            this.cbQueryBoxFontFaces.SelectedItem = ((FontFamily)cbQueryBoxFont.SelectedItem).ChooseRegularFamilyTypeface();

            UserSettingStorage.Instance.Save();
            App.Window.SetTheme(UserSettingStorage.Instance.Theme);
        }

        private void CbQueryBoxFontFaces_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!settingsLoaded) return;
            FamilyTypeface typeface = (FamilyTypeface) cbQueryBoxFontFaces.SelectedItem;
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
                App.Window.SetTheme(UserSettingStorage.Instance.Theme);
            }
        }

        private void CbResultItemFont_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!settingsLoaded) return;
            string resultItemFont = cbResultItemFont.SelectedItem.ToString();
            UserSettingStorage.Instance.ResultItemFont = resultItemFont;
            this.cbResultItemFontFaces.SelectedItem = ((FontFamily)cbResultItemFont.SelectedItem).ChooseRegularFamilyTypeface();

            UserSettingStorage.Instance.Save();
            App.Window.SetTheme(UserSettingStorage.Instance.Theme);
        }

        private void CbResultItemFontFaces_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!settingsLoaded) return;
            FamilyTypeface typeface = (FamilyTypeface)cbResultItemFontFaces.SelectedItem;
            if (typeface == null)
            {
                if (cbResultItemFontFaces.Items.Count > 0)
                    cbResultItemFontFaces.SelectedIndex = 0;

                return;
            }
            else
            {
                UserSettingStorage.Instance.ResultItemFontStretch = typeface.Stretch.ToString();
                UserSettingStorage.Instance.ResultItemFontWeight = typeface.Weight.ToString();
                UserSettingStorage.Instance.ResultItemFontStyle = typeface.Style.ToString();
                UserSettingStorage.Instance.Save();
                App.Window.SetTheme(UserSettingStorage.Instance.Theme);
            }
        }

        private void slOpacity_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            UserSettingStorage.Instance.Opacity = slOpacity.Value;
            UserSettingStorage.Instance.Save();

            if (UserSettingStorage.Instance.OpacityMode == OpacityMode.LayeredWindow)
                PreviewMainPanel.Opacity = UserSettingStorage.Instance.Opacity;
            else
                PreviewMainPanel.Opacity = 1;

            App.Window.SetTheme(UserSettingStorage.Instance.Theme);
        }
        #endregion

        private void CbOpacityMode_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UserSettingStorage.Instance.OpacityMode = (OpacityMode) CbOpacityMode.SelectedItem;
            UserSettingStorage.Instance.Save();

            slOpacity.IsEnabled = UserSettingStorage.Instance.OpacityMode == OpacityMode.LayeredWindow;

            if (UserSettingStorage.Instance.OpacityMode == OpacityMode.LayeredWindow)
                PreviewMainPanel.Opacity = UserSettingStorage.Instance.Opacity;
            else
                PreviewMainPanel.Opacity = 1;
        }

        private void featureBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ISettingProvider provider = null;
            var pair = featureBox.SelectedItem as PluginPair;
            if (pair != null)
            {
                provider = pair.Plugin as ISettingProvider;
                pluginTitle.Text = pair.Metadata.Name;
                pluginSubTitle.Text = pair.Metadata.Description;
                SyntaxSugars.CallOrRescueDefault(
                    () =>
                        pluginIcon.Source =
                            (ImageSource)
                                new ImagePathConverter().Convert(
                                    new object[] {pair.Metadata.IcoPath, pair.Metadata.PluginDirecotry}, null, null,
                                    null));
            }
            else
            {
                provider = featureBox.SelectedItem as ISettingProvider;

                var sys = featureBox.SelectedItem as BaseSystemPlugin;
                if (sys != null)
                {
                    pluginTitle.Text = sys.Name;
                    pluginSubTitle.Text = sys.Description;
                    SyntaxSugars.CallOrRescueDefault(
                        () =>
                            pluginIcon.Source =
                                (ImageSource)
                                    new ImagePathConverter().Convert(
                                        new object[] { sys.IcoPath, sys.PluginDirectory }, null, null,
                                        null));
                }
            }

            this.PluginContentPanel.Content = null;
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
            // featureControls
            // throw new NotImplementedException();
        }
    }
}
