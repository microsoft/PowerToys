using System.Windows;
using System.Linq;

namespace Wox.Plugin.Program
{
    /// <summary>
    /// ProgramSuffixes.xaml 的交互逻辑
    /// </summary>
    public partial class ProgramSuffixes
    {
        private PluginInitContext context;
        private ProgramStorage _settings;

        public ProgramSuffixes(PluginInitContext context, ProgramStorage settings)
        {
            this.context = context;
            InitializeComponent();
            _settings = settings;
            tbSuffixes.Text = string.Join(ProgramSource.SuffixSeperator.ToString(), _settings.ProgramSuffixes);
        }

        private void ButtonBase_OnClick(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(tbSuffixes.Text))
            {
                string warning = context.API.GetTranslation("wox_plugin_program_suffixes_cannot_empty");
                MessageBox.Show(warning);
                return;
            }

            _settings.ProgramSuffixes = tbSuffixes.Text.Split(ProgramSource.SuffixSeperator);
            string msg = context.API.GetTranslation("wox_plugin_program_update_file_suffixes");
            MessageBox.Show(msg);
        }
    }
}
