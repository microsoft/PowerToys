using System.Windows;
using Wox.Infrastructure.Storage.UserSettings;

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

            tbSuffixes.Text = UserSettingStorage.Instance.ProgramSuffixes;
        }

        private void ButtonBase_OnClick(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(tbSuffixes.Text))
            {
                MessageBox.Show("File suffixes can't be empty");
                return;
            }

            UserSettingStorage.Instance.ProgramSuffixes = tbSuffixes.Text;
            MessageBox.Show("Sucessfully update file suffixes");
        }
    }
}
