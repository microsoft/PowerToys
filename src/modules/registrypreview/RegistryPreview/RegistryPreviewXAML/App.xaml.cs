// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Web;
using ManagedCommon;
using Microsoft.PowerToys.Telemetry;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
using RegistryPreview.Telemetry;
using Windows.ApplicationModel.Activation;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.
namespace RegistryPreview
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    public partial class App : Application
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="App"/> class.
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App()
        {
            PowerToysTelemetry.Log.WriteEvent(new RegistryPreviewEditorStartEvent() { TimeStamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() });

            string appLanguage = LanguageHelper.LoadLanguage();

            if (!string.IsNullOrEmpty(appLanguage))
            {
                Microsoft.Windows.Globalization.ApplicationLanguages.PrimaryLanguageOverride = appLanguage;
            }

            this.InitializeComponent();
        }

        /// <summary>
        /// Invoked when the application is launched.
        /// </summary>
        /// <param name="args">Details about the launch request and process.</param>
        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            // Keeping commented out but this is invaluable for protocol activation testing.
            // #if DEBUG
            // System.Diagnostics.Debugger.Launch();
            // #endif

            // Open With... handler - gets activation arguments if they are available.
            AppActivationArguments activatedArgs = AppInstance.GetCurrent().GetActivatedEventArgs();
            if (activatedArgs.Kind == ExtendedActivationKind.File)
            {
                // Covers the double click exe scenario and treated as no file loaded
                AppFilename = string.Empty;
                if (activatedArgs.Data != null)
                {
                    IFileActivatedEventArgs eventArgs = (IFileActivatedEventArgs)activatedArgs.Data;
                    if (eventArgs.Files.Count > 0)
                    {
                        AppFilename = eventArgs.Files[0].Path;
                    }
                }
            }
            else if (activatedArgs.Kind == ExtendedActivationKind.Protocol)
            {
                // When the app is the default handler for REG files and the filename has non-ASCII characters, the app gets activated by Protocol
                AppFilename = string.Empty;
                if (activatedArgs.Data != null)
                {
                    IProtocolActivatedEventArgs eventArgs = (IProtocolActivatedEventArgs)activatedArgs.Data;
                    if (eventArgs.Uri.AbsoluteUri.Length > 0)
                    {
                        AppFilename = eventArgs.Uri.Query.Replace("?ContractId=Windows.File&Verb=open&File=", string.Empty);
                        AppFilename = HttpUtility.UrlDecode(AppFilename);
                    }
                }
            }
            else
            {
                // Right click on a REG file and selected Preview
                // Grab the command line parameters directly from the Environment since this is expected to be run
                // via Context Menu of a REG file.
                string[] cmdArgs = Environment.GetCommandLineArgs();
                if (cmdArgs == null)
                {
                    // Covers the double click exe scenario and treated as no file loaded
                    AppFilename = string.Empty;
                }
                else if (cmdArgs.Length == 2)
                {
                    // GetCommandLineArgs() send in the called EXE as 0 and the selected filename as 1
                    AppFilename = cmdArgs[1];
                }
                else
                {
                    // Anything else should be treated as no file loaded
                    AppFilename = string.Empty;
                }
            }

            // Start the application
            appWindow = new MainWindow();
            appWindow.Activate();
        }

        private Window appWindow;

#pragma warning disable SA1401 // Fields should be private
#pragma warning disable CA2211 // Non-constant fields should not be visible. TODO: consider making it a property
        public static string AppFilename;
#pragma warning restore CA2211 // Non-constant fields should not be visible
#pragma warning restore SA1401 // Fields should be private

        public ETWTrace EtwTrace { get; private set; } = new ETWTrace();
    }
}
