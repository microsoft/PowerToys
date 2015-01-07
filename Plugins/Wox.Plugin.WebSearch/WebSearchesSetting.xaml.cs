using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Wox.Core.UserSettings;

namespace Wox.Plugin.WebSearch
{
    /// <summary>
    /// Interaction logic for WebSearchesSetting.xaml
    /// </summary>
    public partial class WebSearchesSetting : UserControl
    {
        PluginInitContext context;

        public WebSearchesSetting(PluginInitContext context)
        {
            this.context = context;

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
            WebSearchSetting webSearch = new WebSearchSetting(this,context);
            webSearch.ShowDialog();
        }

        private void btnDeleteWebSearch_OnClick(object sender, RoutedEventArgs e)
        {
            Core.UserSettings.WebSearch selectedWebSearch = webSearchView.SelectedItem as Core.UserSettings.WebSearch;
            if (selectedWebSearch != null)
            {
                string msg = string.Format(context.API.GetTranslation("wox_plugin_websearch_delete_warning"),selectedWebSearch.Title);
                    
                if (MessageBox.Show(msg,string.Empty, MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                {
                    UserSettingStorage.Instance.WebSearches.Remove(selectedWebSearch);
                    webSearchView.Items.Refresh();
                }
            }
            else
            {
                string warning =context.API.GetTranslation("wox_plugin_websearch_pls_select_web_search");
                MessageBox.Show(warning);
            }
        }

        private void btnEditWebSearch_OnClick(object sender, RoutedEventArgs e)
        {
            Core.UserSettings.WebSearch selectedWebSearch = webSearchView.SelectedItem as Core.UserSettings.WebSearch;
            if (selectedWebSearch != null)
            {
                WebSearchSetting webSearch = new WebSearchSetting(this,context);
                webSearch.UpdateItem(selectedWebSearch);
                webSearch.ShowDialog();
            }
            else
            {
                string warning = context.API.GetTranslation("wox_plugin_websearch_pls_select_web_search");
                MessageBox.Show(warning);
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
            if (e.AddedItems.Count > 0)
            {
                UserSettingStorage.Instance.WebSearchSuggestionSource =
                    ((ComboBoxItem) e.AddedItems[0]).Content.ToString();
                UserSettingStorage.Instance.Save();
            }
        }
    }
}
