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
    }
}
