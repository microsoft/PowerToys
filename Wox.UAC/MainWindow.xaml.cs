using System;
using System.Reflection;
using System.Windows;

namespace Wox.UAC
{
    public partial class MainWindow : Window
    {
        FileTypeAssociateInstaller installer = new FileTypeAssociateInstaller();

        public MainWindow()
        {
            InitializeComponent();
            string[] param = Environment.GetCommandLineArgs();
            if (param.Length > 1)
            {
                switch (param[1])
                {
                    case "AssociatePluginInstaller":
                        installer.RegisterInstaller();
                        break;
                }
            }
            Application.Current.Shutdown(0);
        }
    }
}
