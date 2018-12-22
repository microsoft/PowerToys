using System.Windows;
using System.Windows.Forms;
using Wox.Plugin.Program.Programs;

namespace Wox.Plugin.Program
{
    /// <summary>
    /// Interaction logic for AddProgramSource.xaml
    /// </summary>
    public partial class AddProgramSource
    {
        private PluginInitContext _context;
        private Settings.ProgramSource _editing;
        private Settings _settings;

        public AddProgramSource(PluginInitContext context, Settings settings)
        {
            InitializeComponent();
            _context = context;
            _settings = settings;
            Directory.Focus();
        }

        public AddProgramSource(Settings.ProgramSource edit, Settings settings)
        {
            _editing = edit;
            _settings = settings;

            InitializeComponent();
            Directory.Text = _editing.Location;
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
            string s = Directory.Text;
            if (!System.IO.Directory.Exists(s))
            {
                System.Windows.MessageBox.Show(_context.API.GetTranslation("wox_plugin_program_invalid_path"));
                return;
            }
            if (_editing == null)
            {
                var source = new Settings.ProgramSource
                {
                    Location = Directory.Text,
                };
                _settings.ProgramSources.Add(source);
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
