using System.Windows;

namespace Wox.Plugin.Program
{
    /// <summary>
    /// ProgramSuffixes.xaml 的交互逻辑
    /// </summary>
    public partial class ProgramSuffixes
    {
        private PluginInitContext context;

        public ProgramSuffixes(PluginInitContext context)
        {
            this.context = context;
            InitializeComponent();

            tbSuffixes.Text = ProgramStorage.Instance.ProgramSuffixes;
        }

        private void ButtonBase_OnClick(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(tbSuffixes.Text))
            {
                string warning = context.API.GetTranslation("wox_plugin_program_suffixes_cannot_empty");
                MessageBox.Show(warning);
                return;
            }

            ProgramStorage.Instance.ProgramSuffixes = tbSuffixes.Text;
            string msg = context.API.GetTranslation("wox_plugin_program_update_file_suffixes");
            MessageBox.Show(msg);
        }
    }
}
