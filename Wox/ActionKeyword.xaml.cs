using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Wox.Core.i18n;
using Wox.Core.Plugin;
using Wox.Infrastructure.Storage.UserSettings;
using Wox.Plugin;
using MessageBox = System.Windows.MessageBox;

namespace Wox
{
    public partial class ActionKeyword : Window
    {
        private PluginMetadata pluginMetadata;

        public ActionKeyword(string pluginId)
        {
            InitializeComponent();
            PluginPair plugin = PluginManager.GetPlugin(pluginId);
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
            tbOldActionKeyword.Text = pluginMetadata.ActionKeyword;
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

            //check new action keyword didn't used by other plugin
            if (PluginManager.AllPlugins.Exists(o => o.Metadata.ActionKeyword == tbAction.Text.Trim()))
            {
                MessageBox.Show(InternationalizationManager.Instance.GetTranslation("newActionKeywordHasBeenAssigned"));
                return;
            }


            pluginMetadata.ActionKeyword = tbAction.Text.Trim();
            var customizedPluginConfig = UserSettingStorage.Instance.CustomizedPluginConfigs.FirstOrDefault(o => o.ID == pluginMetadata.ID);
            if (customizedPluginConfig == null)
            {
                UserSettingStorage.Instance.CustomizedPluginConfigs.Add(new CustomizedPluginConfig()
                {
                    Disabled = false,
                    ID = pluginMetadata.ID,
                    Name = pluginMetadata.Name,
                    Actionword = tbAction.Text.Trim()
                });
            }
            else
            {
                customizedPluginConfig.Actionword = tbAction.Text.Trim();
            }
            UserSettingStorage.Instance.Save();
            MessageBox.Show(InternationalizationManager.Instance.GetTranslation("succeed"));
            Close();
        }
    }
}
