using System.Collections.Generic;
using System.IO;
using System.Windows;
using Microsoft.Win32;
using Wox.Core.Plugin;

namespace Wox.Plugin.WebSearch
{
    public partial class SearchSourceSettingWindow
    {
        private readonly SearchSource _oldSearchSource;
        private SearchSource _searchSource;
        private IList<SearchSource> _searchSources;
        private Action _action;
        private PluginInitContext _context;
        private IPublicAPI _api;
        private SearchSourceViewModel _viewModel;


        public SearchSourceSettingWindow(IList<SearchSource> sources, PluginInitContext context, SearchSource old)
        {
            _oldSearchSource = old;
            _viewModel = new SearchSourceViewModel {SearchSource = old.DeepCopy()};
            Initilize(sources, context, Action.Edit);
        }

        public SearchSourceSettingWindow(IList<SearchSource> sources, PluginInitContext context)
        {
            _viewModel = new SearchSourceViewModel {SearchSource = new SearchSource()};
            Initilize(sources, context, Action.Add);
        }

        private void Initilize(IList<SearchSource> sources, PluginInitContext context, Action action)
        {
            InitializeComponent();
            DataContext = _viewModel;
            _searchSource = _viewModel.SearchSource;
            _searchSources = sources;
            _action = action;
            _context = context;
            _api = _context.API;
        }

        private void OnCancelButtonClick(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void OnConfirmButtonClick(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_searchSource.Title))
            {
                var warning = _api.GetTranslation("wox_plugin_websearch_input_title");
                MessageBox.Show(warning);
            }
            else if (string.IsNullOrEmpty(_searchSource.Url))
            {
                var warning = _api.GetTranslation("wox_plugin_websearch_input_url");
                MessageBox.Show(warning);
            }
            else if (string.IsNullOrEmpty(_searchSource.ActionKeyword))
            {
                var warning = _api.GetTranslation("wox_plugin_websearch_input_action_keyword");
                MessageBox.Show(warning);
            }
            else if (_action == Action.Add)
            {
                AddSearchSource();
            }
            else if (_action == Action.Edit)
            {
                EditSearchSource();
            }
        }

        private void AddSearchSource()
        {
            var keyword = _searchSource.ActionKeyword;
            if (!PluginManager.ActionKeywordRegistered(keyword))
            {
                var id = _context.CurrentPluginMetadata.ID;
                PluginManager.AddActionKeyword(id, keyword);

                _searchSources.Add(_searchSource);

                var info = _api.GetTranslation("success");
                MessageBox.Show(info);
                Close();
            }
            else
            {
                var warning = _api.GetTranslation("newActionKeywordsHasBeenAssigned");
                MessageBox.Show(warning);
            }
        }

        private void EditSearchSource()
        {
            var newKeyword = _searchSource.ActionKeyword;
            var oldKeyword = _oldSearchSource.ActionKeyword;
            if (!PluginManager.ActionKeywordRegistered(newKeyword) || oldKeyword == newKeyword)
            {
                var id = _context.CurrentPluginMetadata.ID;
                PluginManager.ReplaceActionKeyword(id, oldKeyword, newKeyword);

                var index = _searchSources.IndexOf(_oldSearchSource);
                _searchSources[index] = _searchSource;

                var info = _api.GetTranslation("success");
                MessageBox.Show(info);
                Close();
            }
            else
            {
                var warning = _api.GetTranslation("newActionKeywordsHasBeenAssigned");
                MessageBox.Show(warning);
            }
        }

        private void OnSelectIconClick(object sender, RoutedEventArgs e)
        {
            var directory = Main.ImagesDirectory;
            const string filter = "Image files (*.jpg, *.jpeg, *.gif, *.png, *.bmp) |*.jpg; *.jpeg; *.gif; *.png; *.bmp";
            var dialog = new OpenFileDialog {InitialDirectory = directory, Filter = filter};

            var result = dialog.ShowDialog();
            if (result == true)
            {
                var fullpath = dialog.FileName;
                if (!string.IsNullOrEmpty(fullpath))
                {
                    _searchSource.Icon = Path.GetFileName(fullpath);
                    if (!File.Exists(_searchSource.IconPath))
                    {
                        _searchSource.Icon = SearchSource.DefaultIcon;
                        MessageBox.Show($"The file should be put under {directory}");
                    }
                }
            }
        }
    }

    public enum Action
    {
        Add,
        Edit
    }
}