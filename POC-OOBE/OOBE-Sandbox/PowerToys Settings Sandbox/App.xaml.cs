using System;
using Microsoft.QueryStringDotNET;
using PowerToys_Settings_Sandbox.Services;
using PowerToys_Settings_Sandbox.Views;
using Windows.ApplicationModel.Activation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.Storage;

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
            /// <summary>
            /// Sandbox settings is a way to emulate background notifications on install and update
            /// Not to be inclued when added to final product
            /// </summary>
            SandboxNotifications();
            
            if (!args.PrelaunchActivated)
            {
                await ActivationService.ActivateAsync(args); 
            }
           
        }

        /// <summary>
        /// These notifications should be activated in conjunction with new app installs or updates
        /// </summary>
        private void SandboxNotifications()
        {
            var lSettings = ApplicationData.Current.LocalSettings;
            lSettings.Values["IsFirstRun"] = true;
            lSettings.Values["NewVersion"] = "1.0.0.1"; // Reference to the newest version just installed
            lSettings.Values["currentVersion"] = "1.0.0.0"; // Reference to the previous installed version
            Object firstRun = lSettings.Values["IsFirstRun"];
            Object currentVersion = lSettings.Values["currentVersion"];
            Object newVersion = lSettings.Values["NewVersion"];

            /// <summary>
            /// Paste code wherever first run would occur
            /// </summary>
            if (!(firstRun is null) && (bool)firstRun == true)
            {
                NotificationService.AppInstalledToast();
            }
            /// <summary>
            /// Paste code wherever updated app would occur
            /// </summary>
            if (!(currentVersion is null) && (string)currentVersion != (string)newVersion)
            {
                NotificationService.AppUpdatedToast();
            }
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
