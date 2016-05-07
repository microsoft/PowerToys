using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace Wox.Plugin.WebSearch
{
    /// <summary>
    /// Interaction logic for WebSearchesSetting.xaml
    /// </summary>
    public partial class WebSearchesSetting : UserControl
    {
        private Settings _settings;
        public PluginInitContext Context { get; }
        public Main Plugin { get; }

        public WebSearchesSetting(Main plugin, Settings settings)
        {
            Context = plugin.Context;
            Plugin = plugin;
            InitializeComponent();
            Loaded += Setting_Loaded;
            _settings = settings;
        }

        private void Setting_Loaded(object sender, RoutedEventArgs e)
        {
            webSearchView.ItemsSource = _settings.WebSearches;
            cbEnableWebSearchSuggestion.IsChecked = _settings.EnableWebSearchSuggestion;
            comboBoxSuggestionSource.Visibility = _settings.EnableWebSearchSuggestion
                ? Visibility.Visible
                : Visibility.Collapsed;

            List<ComboBoxItem> items = new List<ComboBoxItem>
            {
                new ComboBoxItem {Content = "Google"},
                new ComboBoxItem {Content = "Baidu"}
            };
            ComboBoxItem selected = items.FirstOrDefault(o => o.Content.ToString() == _settings.WebSearchSuggestionSource);
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
            var setting = new WebSearchSetting(this, _settings);
            var webSearch = new WebSearch();
            

            setting.AddItem(webSearch);
            setting.ShowDialog();
        }

        private void btnDeleteWebSearch_OnClick(object sender, RoutedEventArgs e)
        {
            WebSearch selectedWebSearch = webSearchView.SelectedItem as WebSearch;
            if (selectedWebSearch != null)
            {
                string msg = string.Format(Context.API.GetTranslation("wox_plugin_websearch_delete_warning"), selectedWebSearch.Title);

                if (MessageBox.Show(msg, string.Empty, MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                {
                    _settings.WebSearches.Remove(selectedWebSearch);
                    webSearchView.Items.Refresh();
                }
            }
            else
            {
                string warning = Context.API.GetTranslation("wox_plugin_websearch_pls_select_web_search");
                MessageBox.Show(warning);
            }
        }

        private void btnEditWebSearch_OnClick(object sender, RoutedEventArgs e)
        {
            WebSearch selectedWebSearch = webSearchView.SelectedItem as WebSearch;
            if (selectedWebSearch != null)
            {
                WebSearchSetting webSearch = new WebSearchSetting(this, _settings);
                webSearch.UpdateItem(selectedWebSearch);
                webSearch.ShowDialog();
            }
            else
            {
                string warning = Context.API.GetTranslation("wox_plugin_websearch_pls_select_web_search");
                MessageBox.Show(warning);
            }
        }

        private void CbEnableWebSearchSuggestion_OnChecked(object sender, RoutedEventArgs e)
        {
            comboBoxSuggestionSource.Visibility = Visibility.Visible;
            _settings.EnableWebSearchSuggestion = true;
        }

        private void CbEnableWebSearchSuggestion_OnUnchecked(object sender, RoutedEventArgs e)
        {
            comboBoxSuggestionSource.Visibility = Visibility.Collapsed;
            _settings.EnableWebSearchSuggestion = false;
        }

        private void ComboBoxSuggestionSource_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count > 0)
            {
                _settings.WebSearchSuggestionSource = ((ComboBoxItem)e.AddedItems[0]).Content.ToString();
            }
        }
    }
}
