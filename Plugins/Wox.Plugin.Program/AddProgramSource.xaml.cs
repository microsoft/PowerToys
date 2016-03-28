using System.Collections.Generic;
using System.Windows;
using System.Windows.Forms;

namespace Wox.Plugin.Program
{
    /// <summary>
    /// Interaction logic for AddProgramSource.xaml
    /// </summary>
    public partial class AddProgramSource
    {
        private ProgramSource _editing;
        private ProgramStorage _settings;

        public AddProgramSource(ProgramStorage settings)
        {
            _settings = settings;
            InitializeComponent();
            Suffixes.Text = string.Join(";", settings.ProgramSuffixes);
        }

        public AddProgramSource(ProgramSource edit, ProgramStorage settings)
        {
            _editing = edit;
            Directory.Text = _editing.Location;
            MaxDepth.Text = _editing.MaxDepth.ToString();
            Suffixes.Text = string.Join(";", _editing.Suffixes);
            _settings = settings;
            InitializeComponent();
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
                _settings.ProgramSources.Add(new ProgramSource
                {
                    Location = Directory.Text,
                    MaxDepth = max,
                    Suffixes = Suffixes.Text.Split(ProgramSource.SuffixSeperator),
                    Type = "FileSystemProgramSource",
                    Enabled = true
                });
            }
            else
            {
                _editing.Location = Directory.Text;
                _editing.MaxDepth = max;
                _editing.Suffixes = Suffixes.Text.Split(ProgramSource.SuffixSeperator);
            }

            _settings.Save();
            DialogResult = true;
            Close();
        }
    }
}
