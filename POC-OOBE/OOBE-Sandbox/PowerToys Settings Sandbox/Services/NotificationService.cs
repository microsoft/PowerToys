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

            string onActionClick = new QueryString()
                {
                    { "action", "openApp"},
                    { "status", "openFirst"},
                }.ToString();

            ToastActionsCustom actions = createToastAction("Get Started", onActionClick);

            string onLaunch = new QueryString()
                {
                    {"action", "openApp"},
                }.ToString();

            ToastContent toastContent = createToastContent(visual, actions, onLaunch);

            ToastNotification toast = new ToastNotification(toastContent.GetXml());

            toast.ExpirationTime = DateTime.Now.AddDays(2);
            toast.Group = "onInstall";

            ToastNotificationManager.CreateToastNotifier().Show(toast);
        }

        public static void AppUpdatedToast()
        {
            string title = "Power Toys Updated";
            string content = "";
            string logo = "../Assets/MiniLogo.png";

            ToastVisual visual = createToastVisual(title, content, logo);

            string onActionClick = new QueryString()
                {
                    { "action", "openApp"},
                    { "status", "openUpdate"},
                }.ToString();

            ToastActionsCustom actions = createToastAction("See Updates", onActionClick);

            string onLaunch = new QueryString()
                {
                    {"action", "openApp"},
                }.ToString();

            ToastContent toastContent = createToastContent(visual, actions, onLaunch);

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

            string onActionClick = new QueryString()
                {
                    // Action for updating app
                }.ToString();

            ToastActionsCustom actions = createToastAction("Update", onActionClick);

            string onLaunch = new QueryString()
                {
                    {"action", "openApp"},
                }.ToString();

            ToastContent toastContent = createToastContent(visual, actions, onLaunch);

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

        private static ToastActionsCustom createToastAction(string title, string query)
        {
            ToastActionsCustom actions = new ToastActionsCustom()
            {
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
            return actions;
        }

        private static ToastContent createToastContent(ToastVisual visual, ToastActionsCustom actions, String launch)
        {
            ToastContent toastContent = new ToastContent
            {
                Visual = visual,
                Actions = actions,
                Launch = launch,
            };
            return toastContent;
        }

    }

}
