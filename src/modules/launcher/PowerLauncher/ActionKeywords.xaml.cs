// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Windows;
using Wox.Core.Plugin;
using Wox.Core.Resource;
using Wox.Infrastructure.UserSettings;
using Wox.Plugin;

namespace Wox
{
    public partial class ActionKeywords : Window
    {
        private readonly Internationalization _translater = InternationalizationManager.Instance;
        private readonly PluginPair _plugin;

        public ActionKeywords(string pluginId)
        {
            InitializeComponent();
            _plugin = PluginManager.GetPluginForId(pluginId);

            if (_plugin == null)
            {
                MessageBox.Show(_translater.GetTranslation("cannotFindSpecifiedPlugin"));
                Close();
            }
        }

        private void ActionKeyword_OnLoaded(object sender, RoutedEventArgs e)
        {
            tbOldActionKeyword.Text = string.Join(Query.ActionKeywordSeparator, _plugin.Metadata.ActionKeywords.ToArray());
            tbAction.Focus();
        }

        private void BtnCancel_OnClick(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void BtnDone_OnClick(object sender, RoutedEventArgs e)
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
