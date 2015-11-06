using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Media.Imaging;
using Microsoft.Win32;

namespace Wox.Plugin.WebSearch
{
    public partial class WebSearchSetting : Window
    {
        private string defaultWebSearchImageDirectory = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "Images\\websearch");
        private WebSearchesSetting settingWindow;
        private bool update;
        private WebSearch updateWebSearch;
        private PluginInitContext context;

        public WebSearchSetting(WebSearchesSetting settingWidow,PluginInitContext context)
        {
            this.context = context;
            this.settingWindow = settingWidow;
            InitializeComponent();
        }

        public void UpdateItem(WebSearch webSearch)
        {
            updateWebSearch = WebSearchStorage.Instance.WebSearches.FirstOrDefault(o => o == webSearch);
            if (updateWebSearch == null || string.IsNullOrEmpty(updateWebSearch.Url))
            {

                string warning = context.API.GetTranslation("wox_plugin_websearch_invalid_web_search");
                MessageBox.Show(warning);
                Close();
                return;
            }

            update = true;
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
                imgIcon.Source = new BitmapImage(new Uri(path));
            }
            catch (Exception)
            {
            }
        }

        private void BtnCancel_OnClick(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void btnAdd_OnClick(object sender, RoutedEventArgs e)
        {
            string title = tbTitle.Text;
            if (string.IsNullOrEmpty(title))
            {
                string warning = context.API.GetTranslation("wox_plugin_websearch_input_title");
                MessageBox.Show(warning);
                return;
            }

            string url = tbUrl.Text;
            if (string.IsNullOrEmpty(url))
            {
                string warning = context.API.GetTranslation("wox_plugin_websearch_input_url");
                MessageBox.Show(warning);
                return;
            }

            string action = tbActionword.Text;
            if (string.IsNullOrEmpty(action))
            {
                string warning = context.API.GetTranslation("wox_plugin_websearch_input_action_keyword");
                MessageBox.Show(warning);
                return;
            }


            if (!update)
            {
                if (WebSearchStorage.Instance.WebSearches.Exists(o => o.ActionKeyword == action))
                {
                    string warning = context.API.GetTranslation("wox_plugin_websearch_action_keyword_exist");
                    MessageBox.Show(warning);
                    return;
                }
                WebSearchStorage.Instance.WebSearches.Add(new WebSearch()
                {
                    ActionKeyword = action,
                    Enabled = cbEnable.IsChecked ?? false,
                    IconPath = tbIconPath.Text,
                    Url = url,
                    Title = title
                });
                
                //save the action keywords, the order is not metters. Wox will read this metadata when save settings.
                context.CurrentPluginMetadata.ActionKeywords.Add(action);

                string msg = context.API.GetTranslation("wox_plugin_websearch_succeed");
                MessageBox.Show(msg);
            }
            else
            {
                if (WebSearchStorage.Instance.WebSearches.Exists(o => o.ActionKeyword == action && o != updateWebSearch))
                {
                    string warning = context.API.GetTranslation("wox_plugin_websearch_action_keyword_exist");
                    MessageBox.Show(warning);
                    return;
                }
                updateWebSearch.ActionKeyword = action;
                updateWebSearch.IconPath = tbIconPath.Text;
                updateWebSearch.Enabled = cbEnable.IsChecked ?? false;
                updateWebSearch.Url = url;
                updateWebSearch.Title= title;
                
                //save the action keywords, the order is not metters. Wox will read this metadata when save settings.
                context.CurrentPluginMetadata.ActionKeywords.Add(action);

                string msg = context.API.GetTranslation("wox_plugin_websearch_succeed");
                MessageBox.Show(msg);
            }
            WebSearchStorage.Instance.Save();

            settingWindow.ReloadWebSearchView();
            Close();
        }

        private void BtnSelectIcon_OnClick(object sender, RoutedEventArgs e)
        {
            if(!Directory.Exists(defaultWebSearchImageDirectory))
            {
                defaultWebSearchImageDirectory =
                    Path.GetDirectoryName(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location));
            }

            var dlg = new OpenFileDialog
            {
                InitialDirectory = defaultWebSearchImageDirectory,
                Filter ="Image files (*.jpg, *.jpeg, *.gif, *.png, *.bmp) |*.jpg; *.jpeg; *.gif; *.png; *.bmp"
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
