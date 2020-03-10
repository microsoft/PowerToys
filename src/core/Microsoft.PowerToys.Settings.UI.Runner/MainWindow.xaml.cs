using System;
using System.Windows;
using Microsoft.Toolkit.Wpf.UI.XamlHost;
using Microsoft.PowerToys.Settings.UI.Controls;

namespace SettingsRunner
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
            DummyUserControl userControl = windowsXamlHost.GetUwpInternalObject() as DummyUserControl;

            if (userControl != null)
            {
                userControl.XamlIslandMessage = this.WPFMessage;
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
