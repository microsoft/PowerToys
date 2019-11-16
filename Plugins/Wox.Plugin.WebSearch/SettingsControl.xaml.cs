using Microsoft.Win32;
using System.Windows;
using System.Windows.Controls;
using Wox.Core.Plugin;

namespace Wox.Plugin.WebSearch
{
    /// <summary>
    /// Interaction logic for WebSearchesSetting.xaml
    /// </summary>
    public partial class SettingsControl : UserControl
    {
        private readonly Settings _settings;
        private readonly PluginInitContext _context;

        public SettingsControl(PluginInitContext context, SettingsViewModel viewModel)
        {
            InitializeComponent();
            _context = context;
            _settings = viewModel.Settings;
            DataContext = viewModel;
            browserPathBox.Text = _settings.BrowserPath;
            NewWindowBrowser.IsChecked = _settings.OpenInNewBrowser;
            NewTabInBrowser.IsChecked = !_settings.OpenInNewBrowser;
        }

        private void OnAddSearchSearchClick(object sender, RoutedEventArgs e)
        {
            var setting = new SearchSourceSettingWindow(_settings.SearchSources, _context);
            setting.ShowDialog();
        }

        private void OnDeleteSearchSearchClick(object sender, RoutedEventArgs e)
        {
            if (_settings.SelectedSearchSource != null)
            {
                var selected = _settings.SelectedSearchSource;
                var warning = _context.API.GetTranslation("wox_plugin_websearch_delete_warning");
                var formated = string.Format(warning, selected.Title);

                var result = MessageBox.Show(formated, string.Empty, MessageBoxButton.YesNo);
                if (result == MessageBoxResult.Yes)
                {
                    var id = _context.CurrentPluginMetadata.ID;
                    PluginManager.RemoveActionKeyword(id, selected.ActionKeyword);
                    _settings.SearchSources.Remove(selected);
                }
            }
        }

        private void OnEditSearchSourceClick(object sender, RoutedEventArgs e)
        {
            if (_settings.SelectedSearchSource != null)
            {
                var webSearch = new SearchSourceSettingWindow
                (
                    _settings.SearchSources, _context, _settings.SelectedSearchSource
                );

                webSearch.ShowDialog();
            }
        }

        private void OnNewBrowserWindowClick(object sender, RoutedEventArgs e)
        {
            _settings.OpenInNewBrowser = true;
        }

        private void OnNewTabClick(object sender, RoutedEventArgs e)
        {
            _settings.OpenInNewBrowser = false;
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
                _settings.BrowserPath = fileBrowserDialog.FileName;
            }
        }
    }
}
