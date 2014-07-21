using System.Collections.Generic;
using System.Linq;
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
            cbEnableWebSearchSuggestion.IsChecked = UserSettingStorage.Instance.EnableWebSearchSuggestion;
            comboBoxSuggestionSource.Visibility = UserSettingStorage.Instance.EnableWebSearchSuggestion
                ? Visibility.Visible
                : Visibility.Collapsed;
                
            List<ComboBoxItem> items = new List<ComboBoxItem>()
            {
                new ComboBoxItem() {Content = "Google"},
                new ComboBoxItem() {Content = "Bing" },
                new ComboBoxItem() {Content = "Baidu"},
            };
            ComboBoxItem selected = items.FirstOrDefault(o => o.Content.ToString() == UserSettingStorage.Instance.WebSearchSuggestionSource);
            if (selected == null)
            {
                selected = items[0];
            }
            comboBoxSuggestionSource.ItemsSource = items;
            comboBoxSuggestionSource.SelectedItem = selected;
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
            WebSearch selectedWebSearch = webSearchView.SelectedItem as WebSearch;
            if (selectedWebSearch != null)
            {
                if (MessageBox.Show("Are your sure to delete " + selectedWebSearch.Title, "Delete WebSearch",
                    MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                {
                    UserSettingStorage.Instance.WebSearches.Remove(selectedWebSearch);
                    webSearchView.Items.Refresh();
                }
            }
            else
            {
                MessageBox.Show("Please select a web search");
            }
        }

        private void btnEditWebSearch_OnClick(object sender, RoutedEventArgs e)
        {
            WebSearch selectedWebSearch = webSearchView.SelectedItem as WebSearch;
            if (selectedWebSearch != null)
            {
                WebSearchSetting webSearch = new WebSearchSetting(this);
                webSearch.UpdateItem(selectedWebSearch);
                webSearch.ShowDialog();
            }
            else
            {
                MessageBox.Show("Please select a web search");
            }
        }

        private void CbEnableWebSearchSuggestion_OnChecked(object sender, RoutedEventArgs e)
        {
            comboBoxSuggestionSource.Visibility = Visibility.Visible;
            UserSettingStorage.Instance.EnableWebSearchSuggestion = true;
            UserSettingStorage.Instance.Save();
        }

        private void CbEnableWebSearchSuggestion_OnUnchecked(object sender, RoutedEventArgs e)
        {
            comboBoxSuggestionSource.Visibility = Visibility.Collapsed;
            UserSettingStorage.Instance.EnableWebSearchSuggestion = false;
            UserSettingStorage.Instance.Save();
        }

        private void ComboBoxSuggestionSource_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UserSettingStorage.Instance.WebSearchSuggestionSource = ((ComboBoxItem)e.AddedItems[0]).Content.ToString();
            UserSettingStorage.Instance.Save();
        }
    }
}
