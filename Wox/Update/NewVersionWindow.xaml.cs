using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace Wox.Update
{
    /// <summary>
    /// NewVersionWindow.xaml 的交互逻辑
    /// </summary>
    public partial class NewVersionWindow : Window
    {
        public NewVersionWindow()
        {
            InitializeComponent();

            tbCurrentVersion.Text = ConfigurationManager.AppSettings["version"];
            Release newRelease = new UpdateChecker().CheckUpgrade();
            if (newRelease == null)
            {
                tbNewVersion.Visibility = Visibility.Collapsed;
            }
            else
            {
                tbNewVersion.Text = newRelease.version;
            }
        }

        private void tbNewVersion_MouseUp(object sender, MouseButtonEventArgs e)
        {
            Release newRelease = new UpdateChecker().CheckUpgrade();
            if (newRelease != null)
            {
                Process.Start("http://www.getwox.com/release/version/" + newRelease.version);
            }
        }
    }
}
