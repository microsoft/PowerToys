using Microsoft.QueryStringDotNET;
using NotificationsExtensions.Toasts;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Composition;
using Windows.UI.Notifications;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using System;

using PowerToys_Settings_Sandbox.ViewModels;

using Windows.UI.Xaml.Controls;

namespace PowerToys_Settings_Sandbox.Views
{
    public sealed partial class MainPage : Page
    {
        public MainViewModel ViewModel { get; } = new MainViewModel();

        public MainPage()
        {
            InitializeComponent();
            ToastNotify();



        }
        public void  ToastNotify()
        {
            string title = "powertoys installed";
            string content = "check the new features";
            string logo = "Assets/Logo.png";
            string image = "Assets/Logo.scale-200.png";

            ToastVisual visual = new ToastVisual()
            {
                TitleText = new ToastText() { Text = title },
                BodyTextLine1 = new ToastText() { Text = content },
                AppLogoOverride = new ToastAppLogo() { Source = new ToastImageSource(logo) },
                InlineImages = { new ToastImage() { Source = new ToastImageSource(image) } }

            };
            ToastActionsCustom action = new ToastActionsCustom()
            {
                Inputs = { new ToastTextBox("txt") { PlaceholderContent = "write a comment" } },
                Buttons = { new ToastButton("Reply", new QueryString() { "action", "Reply" }.ToString()) }
            };

            ToastContent Content = new ToastContent() { Visual = visual, Actions = action };
            ToastNotification notification = new ToastNotification(Content.GetXml());
            ToastNotificationManager.CreateToastNotifier().Show(notification);
        }
    }
}
