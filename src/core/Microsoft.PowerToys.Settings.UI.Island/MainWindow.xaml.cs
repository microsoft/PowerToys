using System;
using System.Windows;
using Microsoft.Toolkit.Wpf.UI.XamlHost;
using Microsoft.PowerToys.Settings.UI.Controls;
using Microsoft.PowerToys.Settings.UI.Views;
using System.Windows.Input;

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
            ShellPage userControl = windowsXamlHost.GetUwpInternalObject() as ShellPage;

            if (userControl != null)
            {
                //userControl.XamlIslandMessage = this.WPFMessage;
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