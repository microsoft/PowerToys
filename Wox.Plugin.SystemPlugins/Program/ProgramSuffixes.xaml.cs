using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Wox.Infrastructure.Storage.UserSettings;

namespace Wox.Plugin.SystemPlugins.Program
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
