// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Windows;
using Wox.Plugin;

namespace Microsoft.Plugin.Program
{
    /// <summary>
    /// ProgramSuffixes.xaml 的交互逻辑
    /// </summary>
    public partial class ProgramSuffixes
    {
        private PluginInitContext context;
        private ProgramPluginSettings _settings;

        public ProgramSuffixes(PluginInitContext context, ProgramPluginSettings settings)
        {
            this.context = context ?? throw new ArgumentNullException(nameof(context));
            InitializeComponent();
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            tbSuffixes.Text = string.Join(ProgramPluginSettings.SuffixSeparator.ToString(), _settings.ProgramSuffixes);
        }

        private void ButtonBase_OnClick(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(tbSuffixes.Text))
            {
                string warning = context.API.GetTranslation("wox_plugin_program_suffixes_cannot_empty");
                MessageBox.Show(warning);
                return;
            }

            _settings.ProgramSuffixes.Clear();
            _settings.ProgramSuffixes.AddRange(tbSuffixes.Text.Split(ProgramPluginSettings.SuffixSeparator));
            string msg = context.API.GetTranslation("wox_plugin_program_update_file_suffixes");
            MessageBox.Show(msg);

            DialogResult = true;
        }
    }
}
