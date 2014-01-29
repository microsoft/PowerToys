using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Wox.Helper;
using Wox.Infrastructure;

namespace Wox
{
    public partial class SettingWidow : Window
    {
        private MainWindow mainWindow;

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

        private void ButtonBase_OnClick(object sender, RoutedEventArgs e)
        {
            WebSearchSetting webSearch = new WebSearchSetting();
            webSearch.Show();
        }
    }
}
