using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using IWshRuntimeLibrary;
using Microsoft.VisualBasic.ApplicationServices;
using Wox.Infrastructure;
using Wox.Infrastructure.Storage;
using Wox.Infrastructure.Storage.UserSettings;
using Wox.Plugin;
using Wox.Helper;
using Application = System.Windows.Forms.Application;
using File = System.IO.File;
using MessageBox = System.Windows.MessageBox;

namespace Wox
{
    public partial class SettingWindow : Window
    {
        string woxLinkPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Startup), "wox.lnk");
        public MainWindow MainWindow;

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
                    SubTitle = "Search applications, files (via everything plugin) and chrome bookmarks",
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
            webSearchView.ItemsSource = UserSettingStorage.Instance.WebSearches;
            programSourceView.ItemsSource = UserSettingStorage.Instance.ProgramSources;
            lvCustomHotkey.ItemsSource = UserSettingStorage.Instance.CustomPluginHotkeys;
            cbEnablePythonPlugins.IsChecked = UserSettingStorage.Instance.EnablePythonPlugins;
            cbStartWithWindows.IsChecked = File.Exists(woxLinkPath);

            settingsLoaded = true;
            App.Window.SetTheme(UserSettingStorage.Instance.Theme);
        }

        public void ReloadWebSearchView()
        {
            webSearchView.Items.Refresh();
        }

        public void ReloadProgramSourceView()
        {
            programSourceView.Items.Refresh();
        }

        private List<string> LoadAvailableThemes()
        {
            string themePath = Path.Combine(Path.GetDirectoryName(System.Windows.Forms.Application.ExecutablePath), "Themes");
            return Directory.GetFiles(themePath).Where(filePath => filePath.EndsWith(".xaml") && !filePath.EndsWith("Default.xaml")).ToList();
        }



        private void btnAddWebSearch_OnClick(object sender, RoutedEventArgs e)
        {
            WebSearchSetting webSearch = new WebSearchSetting(this);
            webSearch.ShowDialog();
        }

        private void btnDeleteWebSearch_OnClick(object sender, RoutedEventArgs e)
        {
            WebSearch seletedWebSearch = webSearchView.SelectedItem as WebSearch;
            if (seletedWebSearch != null &&
                MessageBox.Show("Are your sure to delete " + seletedWebSearch.Title, "Delete WebSearch",
                    MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                UserSettingStorage.Instance.WebSearches.Remove(seletedWebSearch);
                webSearchView.Items.Refresh();
            }
            else
            {
                MessageBox.Show("Please select a web search");
            }
        }

        private void btnEditWebSearch_OnClick(object sender, RoutedEventArgs e)
        {
            WebSearch seletedWebSearch = webSearchView.SelectedItem as WebSearch;
            if (seletedWebSearch != null)
            {
                WebSearchSetting webSearch = new WebSearchSetting(this);
                webSearch.UpdateItem(seletedWebSearch);
                webSearch.ShowDialog();
            }
            else
            {
                MessageBox.Show("Please select a web search");
            }
        }

        private void btnAddProgramSource_OnClick(object sender, RoutedEventArgs e)
        {
            ProgramSourceSetting programSource = new ProgramSourceSetting(this);
            programSource.ShowDialog();
        }

        private void btnDeleteProgramSource_OnClick(object sender, RoutedEventArgs e)
        {
            ProgramSource seletedProgramSource = programSourceView.SelectedItem as ProgramSource;
            if (seletedProgramSource != null &&
                MessageBox.Show("Are your sure to delete " + seletedProgramSource.ToString(), "Delete ProgramSource",
                    MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                UserSettingStorage.Instance.ProgramSources.Remove(seletedProgramSource);
                programSourceView.Items.Refresh();
            }
            else
            {
                MessageBox.Show("Please select a program source");
            }
        }

        private void btnEditProgramSource_OnClick(object sender, RoutedEventArgs e)
        {
            ProgramSource seletedProgramSource = programSourceView.SelectedItem as ProgramSource;
            if (seletedProgramSource != null)
            {
                ProgramSourceSetting programSource = new ProgramSourceSetting(this);
                programSource.UpdateItem(seletedProgramSource);
                programSource.ShowDialog();
            }
            else
            {
                MessageBox.Show("Please select a program source");
            }
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
            Process.Start("Wox.UAC.exe", "AssociatePluginInstaller");
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
            UserSettingStorage.Instance.QueryBoxFontStretch = typeface.Stretch.ToString();
            UserSettingStorage.Instance.QueryBoxFontWeight = typeface.Weight.ToString();
            UserSettingStorage.Instance.QueryBoxFontStyle = typeface.Style.ToString();
            UserSettingStorage.Instance.Save();
            App.Window.SetTheme(UserSettingStorage.Instance.Theme);
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
            UserSettingStorage.Instance.ResultItemFontStretch = typeface.Stretch.ToString();
            UserSettingStorage.Instance.ResultItemFontWeight = typeface.Weight.ToString();
            UserSettingStorage.Instance.ResultItemFontStyle = typeface.Style.ToString();
            UserSettingStorage.Instance.Save();
            App.Window.SetTheme(UserSettingStorage.Instance.Theme);
        }
        #endregion
    }
}
