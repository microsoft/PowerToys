using System;
using Microsoft.PowerToys.Settings.UI.Activation;
using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.PowerToys.Settings.UI.Services;
using Microsoft.PowerToys.Settings.UI.ViewModels;

using Windows.UI.Xaml.Controls;

namespace Microsoft.PowerToys.Settings.UI.Views
{
    // TODO WTS: Change the icons and titles for all NavigationViewItems in ShellPage.xaml.
    public sealed partial class ShellPage : UserControl
    {
        public delegate void IPCMessageCallback(string msg);

        public ShellViewModel ViewModel { get; } = new ShellViewModel();
        public static Microsoft.UI.Xaml.Controls.NavigationView ShellHandler = null;

        public static IPCMessageCallback Restart_Elevated_Callback = null;
        public static IPCMessageCallback Run_OnStartUp_Callback = null;

        public ShellPage()
        {
            InitializeComponent();
            
            DataContext = ViewModel;
            ShellHandler = navigationView;
            ViewModel.Initialize(shellFrame, navigationView, KeyboardAccelerators);
            NavigationService.Navigate(typeof(MainPage));
            shellFrame.Navigate(typeof(GeneralPage));
        }

        public void SetRestartElevatedCallback(IPCMessageCallback implmentation)
        {
            Restart_Elevated_Callback = implmentation;
        }

        public void SetRunOnStartUpCallback(IPCMessageCallback implmentation)
        {
            Run_OnStartUp_Callback = implmentation;
        }
    }
}
