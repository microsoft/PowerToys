using System;
using Microsoft.QueryStringDotNET;
using PowerToys_Settings_Sandbox.Services;
using PowerToys_Settings_Sandbox.Views;
using Windows.ApplicationModel.Activation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Microsoft.Toolkit.Uwp.Helpers;

namespace PowerToys_Settings_Sandbox
{
    public sealed partial class App : Application
    {
        private Lazy<ActivationService> _activationService;

        private ActivationService ActivationService
        {
            get { return _activationService.Value; }
        }

        public App()
        {
            InitializeComponent();

            // Deferred execution until used. Check https://msdn.microsoft.com/library/dd642331(v=vs.110).aspx for further info on Lazy<T> class.
            _activationService = new Lazy<ActivationService>(CreateActivationService);
        }

        protected override async void OnLaunched(LaunchActivatedEventArgs args)
        {
            if (!args.PrelaunchActivated)
            {
                await ActivationService.ActivateAsync(args); 
            }

            NotificationService.AppInstalledToast();
            NotificationService.AppUpdatedToast();

        }

        protected override async void OnActivated(IActivatedEventArgs e)
        {
            Frame rootFrame = Window.Current.Content as Frame;           

            if (e is ToastNotificationActivatedEventArgs)
            {
                ToastNotificationActivatedEventArgs toastActivationArgs = e as ToastNotificationActivatedEventArgs;

                QueryString args = QueryString.Parse(toastActivationArgs.Argument);

                switch (args["action"])
                {
                    case "OpenApp":
                        if (rootFrame == null)
                        {
                            rootFrame = new Frame();
                            Window.Current.Content = rootFrame;
                        }

                        rootFrame.Navigate(typeof(ShellPage));
                        NavigationService.Navigate(typeof(MainPage), args["status"]);
                        Window.Current.Activate();
                        break;

                    default:
                        break;
                }
            }
        }

        private ActivationService CreateActivationService()
        {
            return new ActivationService(this, typeof(MainPage), new Lazy<UIElement>(CreateShell));
        }

        private UIElement CreateShell()
        {
            return new ShellPage();
        }
    }
}
