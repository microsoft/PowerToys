using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using PowerToys_Settings_Sandbox.Activation;

using Windows.ApplicationModel.Activation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

using Windows.UI.Notifications;
using Microsoft.Toolkit.Uwp.Notifications; // Notifications library
using Microsoft.QueryStringDotNET; // QueryString.NET

namespace PowerToys_Settings_Sandbox.Services
{
    // For more information on understanding and extending activation flow see
    // https://github.com/Microsoft/WindowsTemplateStudio/blob/master/docs/activation.md
    internal class ActivationService
    {
        private readonly App _app;
        private readonly Type _defaultNavItem;
        private Lazy<UIElement> _shell;

        private object _lastActivationArgs;

        private void newNotification()
        {
            
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

            // In a real app, these would be initialized with actual data
            int conversationId = 384928;

            // Construct the actions for the toast (inputs and buttons)
            ToastActionsCustom actions = new ToastActionsCustom()
            {
                Inputs = { }
                Buttons =
                {
                    new ToastButton("Open PowerToys", new QueryString()
                    {
                        { "action", "reply" },
                        { "conversationId", conversationId.ToString() }

                    }.ToString())
                    {
                        ActivationType = ToastActivationType.Background,
                        ImageUri = "Assets/Reply.png",
 
                        // Reference the text box's ID in order to
                        // place this button next to the text box
                        TextBoxId = "tbReply"
                    },
                }

                /*
                Inputs =
                {
                    new ToastTextBox("tbReply")
                    {
                        PlaceholderContent = "Type a response"
                    }
                },

                Buttons =
                {
                    new ToastButton("Reply", new QueryString()
                    {
                        { "action", "reply" },
                        { "conversationId", conversationId.ToString() }

                    }.ToString())
                    {
                        ActivationType = ToastActivationType.Background,
                        ImageUri = "Assets/Reply.png",
 
                        // Reference the text box's ID in order to
                        // place this button next to the text box
                        TextBoxId = "tbReply"
                    },

                    new ToastButton("Like", new QueryString()
                    {
                        { "action", "like" },
                        { "conversationId", conversationId.ToString() }

                    }.ToString())
                    {
                        ActivationType = ToastActivationType.Background
                    },

                    new ToastButton("View", new QueryString()
                    {
                        { "action", "viewImage" },
                        { "imageUrl", image }

                    }.ToString())
                }
                */
            };

            // Now we can construct the final toast content
            ToastContent toastContent = new ToastContent()
            {
                Visual = visual,
                Actions = actions,

                // Arguments when the user taps body of toast
                Launch = new QueryString()
                {
                    { "action", "viewConversation" },
                    { "conversationId", conversationId.ToString() }

                }.ToString()
             };

            // And create the toast notification
            var toast = new ToastNotification(toastContent.GetXml());

            toast.ExpirationTime = DateTime.Now.AddDays(2);

            toast.Tag = "18365";
            toast.Group = "wallPosts";

            ToastNotificationManager.CreateToastNotifier().Show(toast);
        }

        public ActivationService(App app, Type defaultNavItem, Lazy<UIElement> shell = null)
        {
            _app = app;
            _shell = shell;
            _defaultNavItem = defaultNavItem;
        }

        public async Task ActivateAsync(object activationArgs)
        {
            if (IsInteractive(activationArgs))
            {
                // Initialize services that you need before app activation
                // take into account that the splash screen is shown while this code runs.
                await InitializeAsync();

                // Do not repeat app initialization when the Window already has content,
                // just ensure that the window is active
                if (Window.Current.Content == null)
                {
                    // Create a Shell or Frame to act as the navigation context
                    Window.Current.Content = _shell?.Value ?? new Frame();
                }
            }

            // Depending on activationArgs one of ActivationHandlers or DefaultActivationHandler
            // will navigate to the first page
            await HandleActivationAsync(activationArgs);
            _lastActivationArgs = activationArgs;

            if (IsInteractive(activationArgs))
            {
                // Ensure the current window is active
                Window.Current.Activate();

                // Tasks after activation
                await StartupAsync();
            }

            newNotification();

        }

        private async Task InitializeAsync()
        {
            await ThemeSelectorService.InitializeAsync().ConfigureAwait(false);
        }

        private async Task HandleActivationAsync(object activationArgs)
        {
            var activationHandler = GetActivationHandlers()
                                                .FirstOrDefault(h => h.CanHandle(activationArgs));

            if (activationHandler != null)
            {
                await activationHandler.HandleAsync(activationArgs);
            }

            if (IsInteractive(activationArgs))
            {
                var defaultHandler = new DefaultActivationHandler(_defaultNavItem);
                if (defaultHandler.CanHandle(activationArgs))
                {
                    await defaultHandler.HandleAsync(activationArgs);
                }
            }
        }

        private async Task StartupAsync()
        {
            await ThemeSelectorService.SetRequestedThemeAsync();
        }

        private IEnumerable<ActivationHandler> GetActivationHandlers()
        {
            yield break;
        }

        private bool IsInteractive(object args)
        {
            return args is IActivatedEventArgs;
        }
    }
}
