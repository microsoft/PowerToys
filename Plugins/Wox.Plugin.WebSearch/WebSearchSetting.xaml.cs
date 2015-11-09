using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using Wox.Infrastructure.Exception;

namespace Wox.Plugin.WebSearch
{
    public partial class WebSearchSetting : Window
    {
        private string _defaultWebSearchImageDirectory = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "Images\\websearch");
        private readonly WebSearchesSetting _settingWindow;
        private bool _isUpdate;
        private WebSearch _updateWebSearch;
        private readonly PluginInitContext _context;
        private readonly WebSearchPlugin _plguin;

        public WebSearchSetting(WebSearchesSetting settingWidow)
        {
            _plguin = settingWidow.Plugin;
            _context = settingWidow.Context;
            _settingWindow = settingWidow;
            InitializeComponent();
        }

        public void UpdateItem(WebSearch webSearch)
        {
            _updateWebSearch = WebSearchStorage.Instance.WebSearches.FirstOrDefault(o => o == webSearch);
            if (_updateWebSearch == null || string.IsNullOrEmpty(_updateWebSearch.Url))
            {

                string warning = _context.API.GetTranslation("wox_plugin_websearch_invalid_web_search");
                MessageBox.Show(warning);
                Close();
                return;
            }

            _isUpdate = true;
            lblAdd.Text = "Update";
            tbIconPath.Text = webSearch.IconPath;
            ShowIcon(webSearch.IconPath);
            cbEnable.IsChecked = webSearch.Enabled;
            tbTitle.Text = webSearch.Title;
            tbUrl.Text = webSearch.Url;
            tbActionword.Text = webSearch.ActionKeyword;
        }

        private void ShowIcon(string path)
        {
            try
            {
                imgIcon.Source = new BitmapImage(new Uri(path, UriKind.Relative));
            }
            catch (Exception)
            {
            }
        }

        private void BtnCancel_OnClick(object sender, RoutedEventArgs e)
        {
            Close();
        }

        /// <summary>
        /// Confirm button for both add and update
        /// </summary>
        private void btnConfirm_OnClick(object sender, RoutedEventArgs e)
        {
            string title = tbTitle.Text;
            if (string.IsNullOrEmpty(title))
            {
                string warning = _context.API.GetTranslation("wox_plugin_websearch_input_title");
                MessageBox.Show(warning);
                return;
            }

            string url = tbUrl.Text;
            if (string.IsNullOrEmpty(url))
            {
                string warning = _context.API.GetTranslation("wox_plugin_websearch_input_url");
                MessageBox.Show(warning);
                return;
            }

            string newActionKeyword = tbActionword.Text.Trim();
            if (string.IsNullOrEmpty(newActionKeyword))
            {
                string warning = _context.API.GetTranslation("wox_plugin_websearch_input_action_keyword");
                MessageBox.Show(warning);
                return;
            }

            if (_isUpdate)
            {
                try
                {
                    _plguin.NotifyActionKeywordsUpdated(_updateWebSearch.ActionKeyword, newActionKeyword);
                }
                catch (WoxPluginException exception)
                {
                    MessageBox.Show(exception.Message);
                    return;
                }

                _updateWebSearch.ActionKeyword = newActionKeyword;
                _updateWebSearch.IconPath = tbIconPath.Text;
                _updateWebSearch.Enabled = cbEnable.IsChecked ?? false;
                _updateWebSearch.Url = url;
                _updateWebSearch.Title = title;
            }
            else
            {
                try
                {
                    _plguin.NotifyActionKeywordsAdded(newActionKeyword);
                }
                catch (WoxPluginException exception)
                {
                    MessageBox.Show(exception.Message);
                    return;
                }
                WebSearchStorage.Instance.WebSearches.Add(new WebSearch()
                {
                    ActionKeyword = newActionKeyword,
                    Enabled = cbEnable.IsChecked ?? false,
                    IconPath = tbIconPath.Text,
                    Url = url,
                    Title = title
                });
            }

            WebSearchStorage.Instance.Save();
            _settingWindow.ReloadWebSearchView();
            Close();
        }

        private void BtnSelectIcon_OnClick(object sender, RoutedEventArgs e)
        {
            if (!Directory.Exists(_defaultWebSearchImageDirectory))
            {
                _defaultWebSearchImageDirectory =
                    Path.GetDirectoryName(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location));
            }

            var dlg = new OpenFileDialog
            {
                InitialDirectory = _defaultWebSearchImageDirectory,
                Filter = "Image files (*.jpg, *.jpeg, *.gif, *.png, *.bmp) |*.jpg; *.jpeg; *.gif; *.png; *.bmp"
            };

            bool? result = dlg.ShowDialog();
            if (result == true)
            {
                string filename = dlg.FileName;
                tbIconPath.Text = filename;
                ShowIcon(filename);
            }
        }
    }
}
