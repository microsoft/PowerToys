using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Permissions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using Wox.Helper;
using Wox.Infrastructure;
using Wox.Infrastructure.UserSettings;
using MessageBox = System.Windows.MessageBox;

namespace Wox
{
    public partial class SettingWidow : Window
    {
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

            foreach (string theme in LoadAvailableThemes())
            {
                string themeName = theme.Substring(theme.LastIndexOf('\\') + 1).Replace(".xaml", "");
                themeComboBox.Items.Add(themeName);
            }

            themeComboBox.SelectedItem = CommonStorage.Instance.UserSetting.Theme;
            cbReplaceWinR.IsChecked = CommonStorage.Instance.UserSetting.ReplaceWinR;
            webSearchView.ItemsSource = CommonStorage.Instance.UserSetting.WebSearches;
            lvCustomHotkey.ItemsSource = CommonStorage.Instance.UserSetting.CustomPluginHotkeys;
            cbStartWithWindows.IsChecked = CommonStorage.Instance.UserSetting.StartWoxOnSystemStartup;
        }

        public void ReloadWebSearchView()
        {
            webSearchView.Items.Refresh();
        }

        private List<string> LoadAvailableThemes()
        {
            string themePath = Directory.GetCurrentDirectory() + "\\Themes\\";
            return Directory.GetFiles(themePath).Where(filePath => filePath.EndsWith(".xaml")).ToList();
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
            if (!CommonStorage.Instance.UserSetting.StartWoxOnSystemStartup)
            {
                CommonStorage.Instance.UserSetting.StartWoxOnSystemStartup = true;
                OnStartWithWindowsChecked();
                CommonStorage.Instance.Save();
            }
        }

        private void CbStartWithWindows_OnUnchecked(object sender, RoutedEventArgs e)
        {
            CommonStorage.Instance.UserSetting.StartWoxOnSystemStartup = false;
            OnStartWithWindowUnChecked();
            CommonStorage.Instance.Save();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void OnStartWithWindowUnChecked()
        {
            UAC.ExecuteAdminMethod(() => SetStartup(false));
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void OnStartWithWindowsChecked()
        {
            UAC.ExecuteAdminMethod(() => SetStartup(true));
        }

        private void SetStartup(bool startup)
        {
            RegistryKey rk = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);

            if (rk != null)
            {
                if (startup)
                {
                    rk.SetValue("Wox", Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "Wox.exe hidestart"));
                }
                else
                {
                    rk.DeleteValue("Wox", false);
                }
            }
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
                MessageBox.Show("Are your sure to delete " + item.Hotkey + " plugin hotkey?","Delete Custom Plugin Hotkey",
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


    }
}
