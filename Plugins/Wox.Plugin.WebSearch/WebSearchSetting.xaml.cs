using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using Wox.Infrastructure;
using Wox.Infrastructure.Exception;
using Wox.Infrastructure.Image;

namespace Wox.Plugin.WebSearch
{
    public partial class WebSearchSetting : Window
    {
        private const string ImageDirectory = "Images";
        private const string DefaultIcon = "web_search.png";
        private readonly string _pluginDirectory;
        private readonly WebSearchesSetting _settingWindow;
        private bool _isUpdate;
        private WebSearch _webSearch;
        private readonly PluginInitContext _context;
        private readonly WebSearchPlugin _plugin;
        private readonly Settings _settings;

        public WebSearchSetting(WebSearchesSetting settingWidow, Settings settings)
        {
            InitializeComponent();
            _plugin = settingWidow.Plugin;
            _context = settingWidow.Context;
            _settingWindow = settingWidow;
            _settings = settings;
            _pluginDirectory = _settingWindow.Context.CurrentPluginMetadata.PluginDirectory;
        }

        public void UpdateItem(WebSearch webSearch)
        {
            _webSearch = _settings.WebSearches.FirstOrDefault(o => o == webSearch);
            if (_webSearch == null || string.IsNullOrEmpty(_webSearch.Url))
            {

                string warning = _context.API.GetTranslation("wox_plugin_websearch_invalid_web_search");
                MessageBox.Show(warning);
                Close();
                return;
            }

            _isUpdate = true;
            ConfirmButton.Content = "Update";
            WebSearchIcon.Source = LoadIcon(webSearch.IconPath);
            EnableCheckBox.IsChecked = webSearch.Enabled;
            WebSearchName.Text = webSearch.Title;
            Url.Text = webSearch.Url;
            Actionword.Text = webSearch.ActionKeyword;
        }

        public void AddItem(WebSearch websearch)
        {
            _webSearch = websearch;
            if (string.IsNullOrEmpty(_webSearch.IconPath))
            {
                _webSearch.IconPath = DefaultIcon;
                WebSearchIcon.Source = LoadIcon(_webSearch.IconPath);
            }
        }

        private void CancelButtonOnClick(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private ImageSource LoadIcon(string path)
        {
            if (path != null)
            {
                var releativePath = Path.Combine(_pluginDirectory, ImageDirectory, Path.GetFileName(path));
                if (File.Exists(releativePath))
                {
                    _webSearch.IconPath = path;
                    var source = ImageLoader.Load(releativePath);
                    return source;
                }
                else
                {
                    _webSearch.IconPath = path;
                    var source = ImageLoader.Load(path);
                    return source;
                }
            }
            else
            {
                var source = ImageLoader.Load(Path.Combine(_pluginDirectory, ImageDirectory, DefaultIcon));
                return source;
            }

        }

        /// <summary>
        /// Confirm button for both add and update
        /// </summary>
        private void ConfirmButtonOnClick(object sender, RoutedEventArgs e)
        {
            string title = WebSearchName.Text;
            if (string.IsNullOrEmpty(title))
            {
                string warning = _context.API.GetTranslation("wox_plugin_websearch_input_title");
                MessageBox.Show(warning);
                return;
            }

            string url = Url.Text;
            if (string.IsNullOrEmpty(url))
            {
                string warning = _context.API.GetTranslation("wox_plugin_websearch_input_url");
                MessageBox.Show(warning);
                return;
            }

            string newActionKeyword = Actionword.Text.Trim();
            
            if (_isUpdate)
            {
                try
                {
                    _plugin.NotifyActionKeywordsUpdated(_webSearch.ActionKeyword, newActionKeyword);
                }
                catch (WoxPluginException exception)
                {
                    MessageBox.Show(exception.Message);
                    return;
                }
            }
            else
            {
                try
                {
                    _plugin.NotifyActionKeywordsAdded(newActionKeyword);
                }
                catch (WoxPluginException exception)
                {
                    MessageBox.Show(exception.Message);
                    return;
                }

                _settings.WebSearches.Add(_webSearch);
            }

            _webSearch.ActionKeyword = newActionKeyword;
            _webSearch.Enabled = EnableCheckBox.IsChecked ?? false;
            _webSearch.Url = url;
            _webSearch.Title = title;

            _settingWindow.ReloadWebSearchView();
            Close();
        }

        private void SelectIconButtonOnClick(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                InitialDirectory = Path.Combine(_pluginDirectory, ImageDirectory),
                Filter = "Image files (*.jpg, *.jpeg, *.gif, *.png, *.bmp) |*.jpg; *.jpeg; *.gif; *.png; *.bmp"
            };

            bool? result = dlg.ShowDialog();
            if (result != null && result == true)
            {
                string fullpath = dlg.FileName;
                if (fullpath != null)
                {
                    WebSearchIcon.Source = LoadIcon(fullpath);
                }
            }
        }
    }
}
