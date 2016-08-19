using System.Collections.Generic;
using System.Windows;
using System.Windows.Forms;
using Wox.Plugin.Program.ProgramSources;

namespace Wox.Plugin.Program
{
    /// <summary>
    /// Interaction logic for AddProgramSource.xaml
    /// </summary>
    public partial class AddProgramSource
    {
        private FileSystemProgramSource _editing;
        private Settings _settings;

        public AddProgramSource(Settings settings)
        {
            InitializeComponent();
            _settings = settings;
            Directory.Focus();
        }

        public AddProgramSource(FileSystemProgramSource edit, Settings settings)
        {
            _editing = edit;
            _settings = settings;

            InitializeComponent();
            Directory.Text = _editing.Location;
            MaxDepth.Text = _editing.MaxDepth.ToString();
        }

        private void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new FolderBrowserDialog();
            DialogResult result = dialog.ShowDialog();
            if (result == System.Windows.Forms.DialogResult.OK)
            {
                Directory.Text = dialog.SelectedPath;
            }
        }

        private void ButtonAdd_OnClick(object sender, RoutedEventArgs e)
        {
            int max;
            if(!int.TryParse(MaxDepth.Text, out max))
            {
                max = -1;
            }

            if(_editing == null)
            {
                var source = new FileSystemProgramSource
                {
                    Location = Directory.Text,
                    MaxDepth = max,
                };
                _settings.ProgramSources.Add(source);
            }
            else
            {
                _editing.Location = Directory.Text;
                _editing.MaxDepth = max;
            }

            DialogResult = true;
            Close();
        }
    }
}
