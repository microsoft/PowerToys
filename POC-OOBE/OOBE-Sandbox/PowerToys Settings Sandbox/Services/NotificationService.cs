using System;

using Windows.UI.Notifications;
using Microsoft.Toolkit.Uwp.Notifications;
using Microsoft.QueryStringDotNET;

namespace PowerToys_Settings_Sandbox.Services
{
    internal class NotificationService
    {
        public static void AppInstalledToast()
        {
            string title = "Get Started with PowerToys";
            string content = "";
            string logo = "../Assets/MiniLogo.png";

            ToastVisual visual = createToastVisual(title, content, logo);

            ToastActionsCustom actions = new ToastActionsCustom()
            {
                Inputs = { },
                Buttons =
                {
                    new ToastButtonDismiss("Not now"),
                    new ToastButton("Get Started", new QueryString()
                    {
                        {"action", "openApp"},
                        {"status", "openFirst"},
                    }.ToString()),
                }
            };

            ToastContent toastContent = createToastContent(visual, actions);

            ToastNotification toast = new ToastNotification(toastContent.GetXml());

            toast.ExpirationTime = DateTime.Now.AddDays(2);
            toast.Group = "install";

            ToastNotificationManager.CreateToastNotifier().Show(toast);
        }

        public static void AppUpdatedToast()
        {
            string title = "Power Toys Updated";
            string content = "";
            string logo = "../Assets/MiniLogo.png";

            ToastVisual visual = createToastVisual(title, content, logo);

            ToastActionsCustom actions = new ToastActionsCustom()
            {
                Inputs = { },
                Buttons =
                {
                    new ToastButtonDismiss(),
                    new ToastButton("See Updates", new QueryString()
                    {
                        {"action", "openApp"},
                        {"status", "openUpdate"},
                    }.ToString())
                }
            };

            ToastContent toastContent = createToastContent(visual, actions);

            ToastNotification toast = new ToastNotification(toastContent.GetXml());

            toast.ExpirationTime = DateTime.Now.AddDays(2);
            toast.Group = "update";

            ToastNotificationManager.CreateToastNotifier().Show(toast);
        }

        public static void AppNeedsUpdateToast()
        {
            string title = "PowerToys Update Available";
            string content = "";
            string logo = "../Assets/MiniLogo.png";

            ToastVisual visual = createToastVisual(title, content, logo);

            ToastActionsCustom actions = new ToastActionsCustom()
            {
                Inputs = { },
                Buttons =
                {
                    new ToastButtonDismiss(),
                    new ToastButton("Update", new QueryString()
                    {
                        // Query for button
                    }.ToString())
                }
            };

            ToastContent toastContent = createToastContent(visual, actions);

            ToastNotification toast = new ToastNotification(toastContent.GetXml());

            toast.ExpirationTime = DateTime.Now.AddDays(2);
            toast.Group = "updateAvailable";

            ToastNotificationManager.CreateToastNotifier().Show(toast);
        }

        private static ToastVisual createToastVisual(string title, string content, string logo, string image = "")
        {
            ToastVisual visual = new ToastVisual()
            {
                BindingGeneric = new ToastBindingGeneric()
                {
                    Children =
                    {
                        new AdaptiveText()
                        {
                            Text = title
                        },

                        new AdaptiveText()
                        {
                            Text = content
                        },

                        new AdaptiveImage()
                        {
                            Source = image
                        }
                    },

                    AppLogoOverride = new ToastGenericAppLogo()
                    {
                        Source = logo,
                        HintCrop = ToastGenericAppLogoCrop.Default
                    }
                }
            };
            return visual;
        }

        private static ToastContent createToastContent(ToastVisual visual, ToastActionsCustom actions)
        {
            ToastContent toastContent = new ToastContent
            {
                Visual = visual,
                Actions = actions,

                Launch = new QueryString()
                {
                    // Query for arguments when the user taps body of toast
                }.ToString()
            };
            return toastContent;
        }

    }

}
