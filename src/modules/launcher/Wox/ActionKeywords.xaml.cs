using System.Windows;
using Wox.Core.Plugin;
using Wox.Core.Resource;
using Wox.Infrastructure.Exception;
using Wox.Infrastructure.UserSettings;
using Wox.Plugin;

namespace Wox
{
    public partial class ActionKeywords : Window
    {
        private PluginPair _plugin;
        private Settings _settings;
        private readonly Internationalization _translater = InternationalizationManager.Instance;

        public ActionKeywords(string pluginId, Settings settings)
        {
            InitializeComponent();
            _plugin = PluginManager.GetPluginForId(pluginId);
            _settings = settings;
            if (_plugin == null)
            {
                MessageBox.Show(_translater.GetTranslation("cannotFindSpecifiedPlugin"));
                Close();
            }
        }

        private void ActionKeyword_OnLoaded(object sender, RoutedEventArgs e)
        {
            tbOldActionKeyword.Text = string.Join(Query.ActionKeywordSeperater, _plugin.Metadata.ActionKeywords.ToArray());
            tbAction.Focus();
        }

        private void BtnCancel_OnClick(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void btnDone_OnClick(object sender, RoutedEventArgs _)
        {
            var oldActionKeyword = _plugin.Metadata.ActionKeywords[0];
            var newActionKeyword = tbAction.Text.Trim();
            newActionKeyword = newActionKeyword.Length > 0 ? newActionKeyword : "*";
            if (!PluginManager.ActionKeywordRegistered(newActionKeyword))
            {
                var id = _plugin.Metadata.ID;
                PluginManager.ReplaceActionKeyword(id, oldActionKeyword, newActionKeyword);
                MessageBox.Show(_translater.GetTranslation("success"));
                Close();
            }
            else
            {
                string msg = _translater.GetTranslation("newActionKeywordsHasBeenAssigned");
                MessageBox.Show(msg);
            }
        }
    }
}
