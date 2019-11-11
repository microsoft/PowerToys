using Microsoft.Win32;
using System.Windows;
using System.Windows.Controls;
using Wox.Plugin.BrowserBookmark.Models;

namespace Wox.Plugin.BrowserBookmark.Views
{
    /// <summary>
    /// Interaction logic for BrowserBookmark.xaml
    /// </summary>
    public partial class SettingsControl : UserControl
    {
        private readonly Settings _settings;

        public SettingsControl(Settings settings)
        {
            InitializeComponent();
            _settings = settings;
            NewWindowBrowser.IsChecked = _settings.OpenInNewBrowserWindow;
            NewTabInBrowser.IsChecked = !_settings.OpenInNewBrowserWindow;
        }        

        private void OnNewBrowserWindowClick(object sender, RoutedEventArgs e)
        {
            _settings.OpenInNewBrowserWindow = true;
        }

        private void OnNewTabClick(object sender, RoutedEventArgs e)
        {
            _settings.OpenInNewBrowserWindow = false;
        }

        private void OnApplyBTClick(object sender, RoutedEventArgs e)
        {
            _settings.BrowserPath = browserPathBox.Text;
        }

        private void OnChooseClick(object sender, RoutedEventArgs e)
        {
            var fileBrowserDialog = new OpenFileDialog();
            fileBrowserDialog.Filter = "Application(*.exe)|*.exe|All files|*.*";
            fileBrowserDialog.CheckFileExists = true;
            fileBrowserDialog.CheckPathExists = true;
            if (fileBrowserDialog.ShowDialog() == true)
            {
                browserPathBox.Text = fileBrowserDialog.FileName;
            }
        }
    }
}
