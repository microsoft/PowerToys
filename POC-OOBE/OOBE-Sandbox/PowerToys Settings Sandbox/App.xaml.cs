using System;
using Microsoft.QueryStringDotNET;
using PowerToys_Settings_Sandbox.Services;
using PowerToys_Settings_Sandbox.Views;

using Windows.ApplicationModel.Activation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

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
        }

        protected override async void OnActivated(IActivatedEventArgs e)
        {
            //await ActivationService.ActivateAsync(args);
            Frame rootFrame = Window.Current.Content as Frame;

            // TODO: Initialize root frame just like in OnLaunched

            // Handle toast activation
            if (e is ToastNotificationActivatedEventArgs)
            {
                var toastActivationArgs = e as ToastNotificationActivatedEventArgs;

                // Parse the query string (using QueryString.NET)
                QueryString args = QueryString.Parse(toastActivationArgs.Argument);
                var x = args["action"];
                // See what action is being requested 
                switch (x)
                {
                    case "openApp":

                        if (rootFrame == null)
                        {
                            // Create a Frame to act as the navigation context and navigate to the first page
                            rootFrame = new Frame();
                            // Place the frame in the current Window
                            Window.Current.Content = rootFrame;
                        }

                        rootFrame.Navigate(typeof(FancyZonesPage));
                        break;
                }

                // If we're loading the app for the first time, place the main page on
                // the back stack so that user can go back after they've been
                // navigated to the specific page
                //if (rootFrame.BackStack.Count == 0)
                //    rootFrame.BackStack.Add(new PageStackEntry(typeof(MainPage), null, null));
            }

            // TODO: Handle other types of activation

            // Ensure the current window is active
            Window.Current.Activate();
        }



        private ActivationService CreateActivationService()
        {
            return new ActivationService(this, typeof(Views.MainPage), new Lazy<UIElement>(CreateShell));
        }

        private UIElement CreateShell()
        {
            return new Views.ShellPage();
        }
    }
}
