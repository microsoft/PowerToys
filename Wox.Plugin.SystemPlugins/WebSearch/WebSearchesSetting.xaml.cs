using System.Windows;
using System.Windows.Controls;
using Wox.Infrastructure.Storage.UserSettings;

namespace Wox.Plugin.SystemPlugins
{
    /// <summary>
    /// Interaction logic for WebSearchesSetting.xaml
    /// </summary>
    public partial class WebSearchesSetting : UserControl
    {
        public WebSearchesSetting()
        {
            InitializeComponent();

            Loaded += Setting_Loaded;
        }

        private void Setting_Loaded(object sender, RoutedEventArgs e)
        {
            webSearchView.ItemsSource = UserSettingStorage.Instance.WebSearches;
        }

        public void ReloadWebSearchView()
        {
            webSearchView.Items.Refresh();
        }


        private void btnAddWebSearch_OnClick(object sender, RoutedEventArgs e)
        {
            WebSearchSetting webSearch = new WebSearchSetting(this);
            webSearch.ShowDialog();
        }

        private void btnDeleteWebSearch_OnClick(object sender, RoutedEventArgs e)
        {
            WebSearch seletedWebSearch = webSearchView.SelectedItem as WebSearch;
            if (seletedWebSearch != null &&
                MessageBox.Show("Are your sure to delete " + seletedWebSearch.Title, "Delete WebSearch",
                    MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                UserSettingStorage.Instance.WebSearches.Remove(seletedWebSearch);
                webSearchView.Items.Refresh();
            }
            else
            {
                MessageBox.Show("Please select a web search");
            }
        }

        private void btnEditWebSearch_OnClick(object sender, RoutedEventArgs e)
        {
            WebSearch seletedWebSearch = webSearchView.SelectedItem as WebSearch;
            if (seletedWebSearch != null)
            {
                WebSearchSetting webSearch = new WebSearchSetting(this);
                webSearch.UpdateItem(seletedWebSearch);
                webSearch.ShowDialog();
            }
            else
            {
                MessageBox.Show("Please select a web search");
            }
        }

    }
}
