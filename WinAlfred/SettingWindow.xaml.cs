using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using WinAlfred.Helper;

namespace WinAlfred
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
                Settings.Instance.ReplaceWinR = true;
                Settings.Instance.SaveSettings();
            };
            cbReplaceWinR.Unchecked += (o, e) =>
            {
                Settings.Instance.ReplaceWinR = false;
                Settings.Instance.SaveSettings();
            };
        }

        private void Setting_Loaded(object sender, RoutedEventArgs e)
        {
            foreach (string theme in LoadAvailableThemes())
            {
                string themeName = theme.Substring(theme.LastIndexOf('\\') + 1).Replace(".xaml", "");
                themeComboBox.Items.Add(themeName);
            }

            themeComboBox.SelectedItem = Settings.Instance.Theme;
            cbReplaceWinR.IsChecked = Settings.Instance.ReplaceWinR;
        }

        private List<string> LoadAvailableThemes()
        {
            string themePath = Directory.GetCurrentDirectory() + "\\Themes\\";
            return Directory.GetFiles(themePath).Where(filePath => filePath.EndsWith(".xaml")).ToList();
        }

        private void ThemeComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            string themeName = themeComboBox.SelectedItem.ToString();
            mainWindow.ChangeStyles(themeName);
            Settings.Instance.Theme = themeName;
            Settings.Instance.SaveSettings();
        }
    }
}
