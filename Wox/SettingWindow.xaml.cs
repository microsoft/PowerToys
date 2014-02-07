using System.Collections.Generic;
using System.IO;
using System.Linq;
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
        private MainWindow mainWindow;

        public SettingWidow()
        {
            InitializeComponent();
        }

        public SettingWidow(MainWindow mainWindow)
        {
            this.mainWindow = mainWindow;
            InitializeComponent();
            Loaded += Setting_Loaded;
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
        }

        private void Setting_Loaded(object sender, RoutedEventArgs e)
        {
            foreach (string theme in LoadAvailableThemes())
            {
                string themeName = theme.Substring(theme.LastIndexOf('\\') + 1).Replace(".xaml", "");
                themeComboBox.Items.Add(themeName);
            }

            themeComboBox.SelectedItem = CommonStorage.Instance.UserSetting.Theme;
            cbReplaceWinR.IsChecked = CommonStorage.Instance.UserSetting.ReplaceWinR;
            webSearchView.ItemsSource = CommonStorage.Instance.UserSetting.WebSearches;
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
            mainWindow.SetTheme(themeName);
            CommonStorage.Instance.UserSetting.Theme = themeName;
            CommonStorage.Instance.Save();
        }

        private void btnAddWebSearch_OnClick(object sender, RoutedEventArgs e)
        {
            WebSearchSetting webSearch = new WebSearchSetting(this);
            webSearch.Show();
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
                webSearch.Show();
                webSearch.UpdateItem(seletedWebSearch);
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
                    rk.SetValue("Wox", Path.Combine(Directory.GetCurrentDirectory(), "Wox.exe startHide"));
                }
                else
                {
                    rk.DeleteValue("Wox", false);
                }
            }
        }


    }
}
