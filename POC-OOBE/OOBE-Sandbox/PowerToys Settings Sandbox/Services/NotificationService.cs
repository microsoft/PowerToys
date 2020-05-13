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
            string title = "Power Toys";
            string content = "Power Toys is installed";
            string image = "../Assets/Logo.png";
            string logo = "../Assets/MiniLogo.png";

            ToastVisual visual = createToastVisual(title, content, logo, image);

            ToastActionsCustom actions = new ToastActionsCustom()
            {
                Inputs = { },
                Buttons =
                {
                    new ToastButton("Open PowerToys", new QueryString()
                    {
                        {"action", "openApp"},
                        {"imageUrl", "../Assets/Logo.png"}
                    }.ToString()),
                    new ToastButtonDismiss(),
                }
            };

            ToastContent toastContent = createToastContent(visual, actions);

            ToastNotification toast = new ToastNotification(toastContent.GetXml());

            toast.ExpirationTime = DateTime.Now.AddDays(2); // May need to add Tag and group
            toast.Group = "install";

            ToastNotificationManager.CreateToastNotifier().Show(toast);
        }

        public static void AppUpdatedToast()
        {
            string title = "Power Toys";
            string content = "Power Toys has been updated";
            string logo = "../Assets/MiniLogo.png";

            ToastVisual visual = createToastVisual(title, content, logo);

            ToastActionsCustom actions = new ToastActionsCustom()
            {
                Inputs = { },
                Buttons =
                {

                    new ToastButton("Open PowerToys", new QueryString()
                    {
                        // Query for button
                    }.ToString())
                    {
                        // Insert activation type here
                    },
                    new ToastButtonDismiss(),
                }
            };

            ToastContent toastContent = createToastContent(visual, actions);

            ToastNotification toast = new ToastNotification(toastContent.GetXml());

            toast.ExpirationTime = DateTime.Now.AddDays(2); // May need to add Tag and group
            toast.Group = "update";

            ToastNotificationManager.CreateToastNotifier().Show(toast);
        }

        public static void AppNeedsUpdateToast()
        {
            string title = "Power Toys";
            string content = "Power Toys has a new update available";
            string logo = "../Assets/MiniLogo.png";

            ToastVisual visual = createToastVisual(title, content, logo);

            ToastActionsCustom actions = new ToastActionsCustom()
            {
                Inputs = { },
                Buttons =
                {

                    new ToastButton("Update Now", new QueryString()
                    {
                        // Query for button
                    }.ToString())
                    {
                        // Insert activation type here
                    },
                    new ToastButtonSnooze(),
                }
            };

            ToastContent toastContent = createToastContent(visual, actions);

            ToastNotification toast = new ToastNotification(toastContent.GetXml());

            toast.ExpirationTime = DateTime.Now.AddDays(2); // May need to add Tag and group
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
