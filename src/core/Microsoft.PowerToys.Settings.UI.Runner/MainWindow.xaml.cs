using System;
using System.Windows;
using Microsoft.Toolkit.Wpf.UI.XamlHost;
using Microsoft.PowerToys.Settings.UI.Controls;
using Microsoft.PowerToys.Settings.UI.Views;
using System.Threading;

namespace Microsoft.PowerToys.Settings.UI.Runner
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void WindowsXamlHost_ChildChanged(object sender, EventArgs e)
        {
            // Hook up x:Bind source.
            WindowsXamlHost windowsXamlHost = sender as WindowsXamlHost;
            ShellPage shellPage = windowsXamlHost.GetUwpInternalObject() as ShellPage;

            if (shellPage != null)
            {
                // set restart as admin configuration and restart.
                shellPage.SetRestartElevatedCallback(delegate (string msg)
                {
                    Program.ipcmanager.SendMessage(msg);

                    int milliseconds = 2000;
                    Thread.Sleep(milliseconds);

                    System.Windows.Application.Current.Shutdown();
                });

                // send the rest of the settings without restarting.
                shellPage.SetDefaultSndMessageCallback(delegate (string msg)
                {
                    Program.ipcmanager.SendMessage(msg);
                });

            }
        }

        public string WPFMessage
        {
            get
            {
                return "Binding from WPF to UWP XAML";
            }
        }
    }
}
