using System.Windows;
using Wox.Core.Plugin;
using Wox.Core.Resource;
using Wox.Core.UserSettings;
using Wox.Infrastructure.Exception;
using Wox.Plugin;

namespace Wox
{
    public partial class ActionKeywords : Window
    {
        private PluginPair _plugin;
        private Settings _settings;

        public ActionKeywords(string pluginId, Settings settings)
        {
            InitializeComponent();
            _plugin = PluginManager.GetPluginForId(pluginId);
            _settings = settings;
            if (_plugin == null)
            {
                MessageBox.Show(InternationalizationManager.Instance.GetTranslation("cannotFindSpecifiedPlugin"));
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
            try
            {
                // update in-memory data
                PluginManager.UpdateActionKeywordForPlugin(_plugin, oldActionKeyword, newActionKeyword);
            }
            catch (WoxPluginException e)
            {
                MessageBox.Show(e.Message);
                return;
            }
            // update persistant data
            _settings.UpdateActionKeyword(_plugin.Metadata);

            MessageBox.Show(InternationalizationManager.Instance.GetTranslation("succeed"));
            Close();
        }
    }
}
