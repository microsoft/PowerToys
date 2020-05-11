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
    internal class Notifications {
        public static void Toast() {

            // In a real app, these would be initialized with actual data
            string title = "Power Toys";
            string content = "Power Toys is installed";
            string image = "../Assets/Logo.png";
            string logo = "../Assets/MiniLogo.png";

            // Construct the visuals of the toast
            ToastVisual visual = new ToastVisual()
            {
                BindingGeneric = new ToastBindingGeneric()
                {
                    Children =
                    {
                        new AdaptiveText() {
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
                        HintCrop = ToastGenericAppLogoCrop.Circle
                    }
                }
            };

            // Construct the actions for the toast (inputs and buttons)
            ToastActionsCustom actions = new ToastActionsCustom()
            {
                Inputs = { },
                Buttons =
                {
                    new ToastButton("Open PowerToys", new QueryString()
                    {
                        // Insert query here
                    }.ToString())
                    {
                        // Insert activation type here
                    },
                }
            };

            // Now we can construct the final toast content
            ToastContent toastContent = new ToastContent()
            {
                Visual = visual,
                Actions = actions,

                // Arguments when the user taps body of toast
                Launch = new QueryString()
                {
                    // Insert query here
                }.ToString()
            };

            // And create the toast notification
            var toast = new ToastNotification(toastContent.GetXml());

            toast.ExpirationTime = DateTime.Now.AddDays(2); // May need to add Tag and group

            ToastNotificationManager.CreateToastNotifier().Show(toast);
        }

    }

}
