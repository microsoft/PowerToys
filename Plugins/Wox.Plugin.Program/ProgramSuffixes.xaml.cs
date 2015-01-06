using System.Windows;

namespace Wox.Plugin.Program
{
    /// <summary>
    /// ProgramSuffixes.xaml 的交互逻辑
    /// </summary>
    public partial class ProgramSuffixes
    {
        public ProgramSuffixes()
        {
            InitializeComponent();

            tbSuffixes.Text = ProgramStorage.Instance.ProgramSuffixes;
        }

        private void ButtonBase_OnClick(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(tbSuffixes.Text))
            {
                MessageBox.Show("File suffixes can't be empty");
                return;
            }

            ProgramStorage.Instance.ProgramSuffixes = tbSuffixes.Text;
            MessageBox.Show("Sucessfully update file suffixes");
        }
    }
}
