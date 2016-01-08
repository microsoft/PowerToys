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

        public AddProgramSource()
        {
            InitializeComponent();
        }

        public AddProgramSource(ProgramSource edit) : this()
        {
            _editing = edit;
            Directory.Text = _editing.Location;
            MaxDepth.Text = _editing.MaxDepth.ToString();
            Suffixes.Text = _editing.Suffixes;
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
                ProgramStorage.Instance.ProgramSources.Add(new ProgramSource
                {
                    Location = Directory.Text,
                    MaxDepth = max,
                    Suffixes = Suffixes.Text,
                    Type = "FileSystemProgramSource",
                    Enabled = true
                });
            }
            else
            {
                _editing.Location = Directory.Text;
                _editing.MaxDepth = max;
                _editing.Suffixes = Suffixes.Text;
            }

            ProgramStorage.Instance.Save();
            DialogResult = true;
            Close();
        }
    }
}
