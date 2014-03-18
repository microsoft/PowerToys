using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using IWshRuntimeLibrary;
using Wox.Infrastructure;
using Wox.Infrastructure.UserSettings;
using Application = System.Windows.Forms.Application;
using File = System.IO.File;
using MessageBox = System.Windows.MessageBox;

namespace Wox
{
    public partial class SettingWidow : Window
    {
        string woxLinkPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Startup), "wox.lnk");
        public MainWindow MainWindow;

        public SettingWidow()
        {
            InitializeComponent();
        }

        public SettingWidow(MainWindow mainWindow)
        {
            this.MainWindow = mainWindow;
            InitializeComponent();
            Loaded += Setting_Loaded;
        }

        private void Setting_Loaded(object sender, RoutedEventArgs ev)
        {
            ctlHotkey.OnHotkeyChanged += ctlHotkey_OnHotkeyChanged;
            ctlHotkey.SetHotkey(CommonStorage.Instance.UserSetting.Hotkey, false);
            cbReplaceWinR.Checked += (o, e) =>
            {
                CommonStorage.Instance.UserSetting.ReplaceWinR = true;
                CommonStorage.Instance.Save();
            };
            cbReplaceWinR.Unchecked += (o, e) =>
            {
                CommonStorage.Instance.UserSetting.ReplaceWinR = false;
                CommonStorage.Instance.Save();
            };

            cbEnablePythonPlugins.Checked += (o, e) =>
            {
                CommonStorage.Instance.UserSetting.EnablePythonPlugins = true;
                CommonStorage.Instance.Save();
            };
            cbEnablePythonPlugins.Unchecked += (o, e) =>
            {
                CommonStorage.Instance.UserSetting.EnablePythonPlugins = false;
                CommonStorage.Instance.Save();
            };


            foreach (string theme in LoadAvailableThemes())
            {
                string themeName = theme.Substring(theme.LastIndexOf('\\') + 1).Replace(".xaml", "");
                themeComboBox.Items.Add(themeName);
            }

            themeComboBox.SelectedItem = CommonStorage.Instance.UserSetting.Theme;
            cbReplaceWinR.IsChecked = CommonStorage.Instance.UserSetting.ReplaceWinR;
            webSearchView.ItemsSource = CommonStorage.Instance.UserSetting.WebSearches;
            lvCustomHotkey.ItemsSource = CommonStorage.Instance.UserSetting.CustomPluginHotkeys;
            cbEnablePythonPlugins.IsChecked = CommonStorage.Instance.UserSetting.EnablePythonPlugins;
            cbStartWithWindows.IsChecked = File.Exists(woxLinkPath);
        }

        public void ReloadWebSearchView()
        {
            webSearchView.Items.Refresh();
        }

        private List<string> LoadAvailableThemes()
        {
            string themePath = Directory.GetCurrentDirectory() + "\\Themes\\";
            return Directory.GetFiles(themePath).Where(filePath => filePath.EndsWith(".xaml") && !filePath.EndsWith("Default.xaml")).ToList();
        }

        private void ThemeComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            string themeName = themeComboBox.SelectedItem.ToString();
            MainWindow.SetTheme(themeName);
            CommonStorage.Instance.UserSetting.Theme = themeName;
            CommonStorage.Instance.Save();
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
                CommonStorage.Instance.UserSetting.WebSearches.Remove(seletedWebSearch);
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

        private void CbStartWithWindows_OnChecked(object sender, RoutedEventArgs e)
        {
            CreateStartupFolderShortcut();
            CommonStorage.Instance.UserSetting.StartWoxOnSystemStartup = true;
            CommonStorage.Instance.Save();
        }

        private void CbStartWithWindows_OnUnchecked(object sender, RoutedEventArgs e)
        {
            if (File.Exists(woxLinkPath))
            {
                File.Delete(woxLinkPath);
            }

            CommonStorage.Instance.UserSetting.StartWoxOnSystemStartup = false;
            CommonStorage.Instance.Save();
        }

        private void CreateStartupFolderShortcut()
        {
            WshShellClass wshShell = new WshShellClass();

            IWshShortcut shortcut = (IWshShortcut)wshShell.CreateShortcut(woxLinkPath);
            shortcut.TargetPath = Application.ExecutablePath;
            shortcut.Arguments = "hideStart";
            shortcut.WorkingDirectory = Application.StartupPath;
            shortcut.Description = "Launch Wox";
            shortcut.IconLocation = Application.StartupPath + @"\App.ico";
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
                MainWindow.RemoveHotkey(CommonStorage.Instance.UserSetting.Hotkey);
                CommonStorage.Instance.UserSetting.Hotkey = ctlHotkey.CurrentHotkey.ToString();
                CommonStorage.Instance.Save();
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
                CommonStorage.Instance.UserSetting.CustomPluginHotkeys.Remove(item);
                lvCustomHotkey.Items.Refresh();
                CommonStorage.Instance.Save();
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
    }
}
