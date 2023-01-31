using Microsoft.UI.Xaml;
using System;
using System.Diagnostics;
using Windows.ApplicationModel.Activation;
using LaunchActivatedEventArgs = Windows.ApplicationModel.Activation.LaunchActivatedEventArgs;

namespace RegistryPreview
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    public partial class App : Application
    {
        /// <summary>
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App()
        {
            this.InitializeComponent();
        }

        /// <summary>
        /// Invoked when the application is launched normally by the end user.  Other entry points
        /// will be used such as when the application is launched to open a specific file.
        /// </summary>
        /// <param name="args">Details about the launch request and process.</param>
        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            // Grab the command line parameters directly from the Environment since this is expected to be run
            // via Context Menu of a REG file.
            string[] cmdArgs = Environment.GetCommandLineArgs();
            if (cmdArgs == null)
            {
                // Covers the doubleclick exe scenario and treated as no file loaded
                s_Filename = "";
            }
            else if (cmdArgs.Length == 2)
            {
                // GetCommandLineArgs() send in the called EXE as 0 and the selected filename as 1
                s_Filename = cmdArgs[1];
            }
            else 
            {
                // Anything else should be treated as no file loaded
                s_Filename = "";
            }

            // Start the application
            m_window = new MainWindow();
            m_window.Activate();
        }

        private Window m_window;
        public static string s_Filename= "";
    }
}
