// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

using ManagedCommon;
using Microsoft.UI.Xaml;

namespace KeystrokeOverlayUI
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    public partial class App : Application, IDisposable
    {
        private MainWindow window;

        /// <summary>
        /// Initializes a new instance of the <see cref="App"/> class.
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App()
        {
            string appLanguage = LanguageHelper.LoadLanguage();
            if (!string.IsNullOrEmpty(appLanguage))
            {
                Microsoft.Windows.Globalization.ApplicationLanguages.PrimaryLanguageOverride = appLanguage;
            }

            Logger.InitializeLogger("\\KeystrokeOverlay\\Logs");

            this.InitializeComponent();

            UnhandledException += App_UnhandledException;
        }

        /// <summary>
        /// Invoked when the application is launched normally by the end user.
        /// </summary>
        /// <param name="args">Details about the launch request and process.</param>
        protected override void OnLaunched(LaunchActivatedEventArgs args)
        {
            // if (PowerToys.GPOWrapper.GPOWrapper.GetConfiguredKeystrokeOverlayEnabledValue() == PowerToys.GPOWrapper.GpoRuleConfigured.Disabled)
            // {
            //     Logger.LogWarning("Tried to start with a GPO policy setting the utility to always be disabled.");
            //     Environment.Exit(0);
            //     return;
            // }
            window = new MainWindow();
            window.Activate();
        }

        public void Dispose()
        {
            window?.Dispose();
            GC.SuppressFinalize(this);
        }

        private void App_UnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
        {
            ManagedCommon.Logger.LogError("Unhandled exception", e.Exception);
        }
    }
}
