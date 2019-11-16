using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Microsoft.Win32;


namespace Wox.Plugin.Url
{
    /// <summary>
    /// SettingsControl.xaml 的交互逻辑
    /// </summary>
    public partial class SettingsControl : UserControl
    {
        private Settings _settings;
        private IPublicAPI _woxAPI;

        public SettingsControl(IPublicAPI woxAPI,Settings settings)
        {
            InitializeComponent();
            _settings = settings;
            _woxAPI = woxAPI;
            browserPathBox.Text = _settings.BrowserPath;
            NewWindowBrowser.IsChecked = _settings.OpenInNewBrowserWindow;
            NewTabInBrowser.IsChecked = !_settings.OpenInNewBrowserWindow;
        }

        private void OnChooseClick(object sender, RoutedEventArgs e)
        {
            var fileBrowserDialog = new OpenFileDialog();
            fileBrowserDialog.Filter = _woxAPI.GetTranslation("wox_plugin_url_plugin_filter"); ;
            fileBrowserDialog.CheckFileExists = true;
            fileBrowserDialog.CheckPathExists = true;
            if (fileBrowserDialog.ShowDialog() == true)
            {
                browserPathBox.Text = fileBrowserDialog.FileName;
                _settings.BrowserPath = fileBrowserDialog.FileName;
            }
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
