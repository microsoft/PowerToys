// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;
using System.Windows;
using System.Windows.Forms;
using Microsoft.Plugin.Program.Views;
using Wox.Plugin;

namespace Microsoft.Plugin.Program
{
    /// <summary>
    /// Interaction logic for AddProgramSource.xaml
    /// </summary>
    public partial class AddProgramSource
    {
        private PluginInitContext _context;
        private ProgramSource _editing;
        private ProgramPluginSettings _settings;

        public AddProgramSource(PluginInitContext context, ProgramPluginSettings settings)
        {
            InitializeComponent();
            _context = context;
            _settings = settings;
            Directory.Focus();
        }

        public AddProgramSource(ProgramSource edit, ProgramPluginSettings settings)
        {
            _editing = edit ?? throw new ArgumentNullException(nameof(edit));
            _settings = settings;

            InitializeComponent();
            Directory.Text = _editing.Location;
        }

        private void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            using (var dialog = new FolderBrowserDialog())
            {
                DialogResult result = dialog.ShowDialog();
                if (result == System.Windows.Forms.DialogResult.OK)
                {
                    Directory.Text = dialog.SelectedPath;
                }
            }
        }

        private void ButtonAdd_OnClick(object sender, RoutedEventArgs e)
        {
            string s = Directory.Text;
            if (!System.IO.Directory.Exists(s))
            {
                System.Windows.MessageBox.Show(_context.API.GetTranslation("wox_plugin_program_invalid_path"));
                return;
            }

            if (_editing == null)
            {
                if (!ProgramSetting.ProgramSettingDisplayList.Any(x => x.UniqueIdentifier == Directory.Text))
                {
                    var source = new ProgramSource
                    {
                        Location = Directory.Text,
                        UniqueIdentifier = Directory.Text,
                    };

                    _settings.ProgramSources.Insert(0, source);
                    ProgramSetting.ProgramSettingDisplayList.Add(source);
                }
            }
            else
            {
                _editing.Location = Directory.Text;
            }

            DialogResult = true;
            Close();
        }
    }
}
