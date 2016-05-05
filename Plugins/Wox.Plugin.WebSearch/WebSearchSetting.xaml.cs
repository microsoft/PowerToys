using System;
using System.IO;
using System.Linq;
using System.Windows;
using Microsoft.Win32;
using Wox.Infrastructure.Exception;
using Wox.Infrastructure.Image;

namespace Wox.Plugin.WebSearch
{
    public partial class WebSearchSetting
    {
        private readonly WebSearchesSetting _settingWindow;
        private bool _isUpdate;
        private WebSearch _webSearch;
        private readonly PluginInitContext _context;
        private readonly WebSearchPlugin _plugin;
        private readonly Settings _settings;

        public WebSearchSetting(WebSearchesSetting settingWidow, Settings settings)
        {
            InitializeComponent();
            WebSearchName.Focus();
            _plugin = settingWidow.Plugin;
            _context = settingWidow.Context;
            _settingWindow = settingWidow;
            _settings = settings;
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
            WebSearchIcon.Source = ImageLoader.Load(webSearch.IconPath);
            EnableCheckBox.IsChecked = webSearch.Enabled;
            WebSearchName.Text = webSearch.Title;
            Url.Text = webSearch.Url;
            Actionword.Text = webSearch.ActionKeyword;
        }

        public void AddItem(WebSearch webSearch)
        {
            _webSearch = webSearch;
            WebSearchIcon.Source = ImageLoader.Load(webSearch.IconPath);
        }

        private void CancelButtonOnClick(object sender, RoutedEventArgs e)
        {
            Close();
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
            var directory = Path.Combine(WebSearchPlugin.PluginDirectory, WebSearchPlugin.ImageDirectory);
            var dlg = new OpenFileDialog
            {
                InitialDirectory = directory,
                Filter = "Image files (*.jpg, *.jpeg, *.gif, *.png, *.bmp) |*.jpg; *.jpeg; *.gif; *.png; *.bmp"
            };

            bool? result = dlg.ShowDialog();
            if (result != null && result == true)
            {
                string fullpath = dlg.FileName;
                if (fullpath != null)
                {
                    _webSearch.Icon = Path.GetFileName(fullpath);
                    if (File.Exists(_webSearch.IconPath))
                    {
                        WebSearchIcon.Source = ImageLoader.Load(_webSearch.IconPath);
                    }
                    else
                    {
                        _webSearch.Icon = WebSearch.DefaultIcon;
                        MessageBox.Show($"The file should be put under {directory}");
                    }
                }
            }
        }
    }
}
