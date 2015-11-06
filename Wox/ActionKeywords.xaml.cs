using System;
using System.Linq;
using System.Windows;
using Wox.Core.i18n;
using Wox.Core.Plugin;
using Wox.Core.UserSettings;
using Wox.Plugin;

namespace Wox
{
    public partial class ActionKeywords : Window
    {
        private PluginMetadata pluginMetadata;

        public ActionKeywords(string pluginId)
        {
            InitializeComponent();
            PluginPair plugin = PluginManager.GetPluginForId(pluginId);
            if (plugin == null)
            {
                MessageBox.Show(InternationalizationManager.Instance.GetTranslation("cannotFindSpecifiedPlugin"));
                Close();
                return;
            }

            pluginMetadata = plugin.Metadata;
        }

        private void ActionKeyword_OnLoaded(object sender, RoutedEventArgs e)
        {
            tbOldActionKeyword.Text = string.Join(Query.ActionKeywordSeperater, pluginMetadata.ActionKeywords.ToArray());
            tbAction.Focus();
        }

        private void BtnCancel_OnClick(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void btnDone_OnClick(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(tbAction.Text))
            {
                MessageBox.Show(InternationalizationManager.Instance.GetTranslation("newActionKeywordCannotBeEmpty"));
                return;
            }

            var actionKeywords = tbAction.Text.Trim().Split(new[] { Query.ActionKeywordSeperater }, StringSplitOptions.RemoveEmptyEntries).ToList();
            //check new action keyword didn't used by other plugin
            if (actionKeywords[0] != Query.GlobalPluginWildcardSign && PluginManager.AllPlugins.
                                        SelectMany(p => p.Metadata.ActionKeywords).
                                        Any(k => actionKeywords.Contains(k)))
            {
                MessageBox.Show(InternationalizationManager.Instance.GetTranslation("newActionKeywordHasBeenAssigned"));
                return;
            }


            pluginMetadata.ActionKeywords = actionKeywords;
            var customizedPluginConfig = UserSettingStorage.Instance.CustomizedPluginConfigs.FirstOrDefault(o => o.ID == pluginMetadata.ID);
            if (customizedPluginConfig == null)
            {
                UserSettingStorage.Instance.CustomizedPluginConfigs.Add(new CustomizedPluginConfig()
                {
                    Disabled = false,
                    ID = pluginMetadata.ID,
                    Name = pluginMetadata.Name,
                    ActionKeywords = actionKeywords
                });
            }
            else
            {
                customizedPluginConfig.ActionKeywords = actionKeywords;
            }
            UserSettingStorage.Instance.Save();
            MessageBox.Show(InternationalizationManager.Instance.GetTranslation("succeed"));
            Close();
        }
    }
}
