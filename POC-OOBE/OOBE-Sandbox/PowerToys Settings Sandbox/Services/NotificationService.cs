using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Windows.UI.Notifications;
using Microsoft.Toolkit.Uwp.Notifications; // Notifications library
using Microsoft.QueryStringDotNET; // QueryString.NET

namespace PowerToys_Settings_Sandbox.Services
{
    internal class NotificationService {
        public static void AppInstalledToast() {

            string title = "Power Toys";
            string content = "Power Toys is installed";
            string image = "../Assets/Logo.png";
            string logo = "../Assets/MiniLogo.png";

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
                }
            };

            ToastContent toastContent = new ToastContent()
            {
                Visual = visual,
                Actions = actions,

                Launch = new QueryString()
                {
                    // Query for arguments when the user taps body of toast
                }.ToString()
            };

            ToastNotification toast = new ToastNotification(toastContent.GetXml());

            toast.ExpirationTime = DateTime.Now.AddDays(2); // May need to add Tag and group

            ToastNotificationManager.CreateToastNotifier().Show(toast);
        }

    }

}
