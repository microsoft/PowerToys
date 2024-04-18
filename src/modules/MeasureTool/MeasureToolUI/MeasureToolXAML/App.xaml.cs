// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using ManagedCommon;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;

namespace MeasureToolUI
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
            Logger.InitializeLogger("\\Measure Tool\\MeasureToolUI\\Logs");

            this.InitializeComponent();
        }

        /// <summary>
        /// Invoked when the application is launched normally by the end user.  Other entry points
        /// will be used such as when the application is launched to open a specific file.
        /// </summary>
        /// <param name="args">Details about the launch request and process.</param>
        protected override void OnLaunched(LaunchActivatedEventArgs args)
        {
            if (PowerToys.GPOWrapper.GPOWrapper.GetConfiguredScreenRulerEnabledValue() == PowerToys.GPOWrapper.GpoRuleConfigured.Disabled)
            {
                Logger.LogWarning("Tried to start with a GPO policy setting the utility to always be disabled. Please contact your systems administrator.");
                Environment.Exit(0); // Current.Exit won't work until there's a window opened.
                return;
            }

            var cmdArgs = Environment.GetCommandLineArgs();
            if (cmdArgs?.Length > 1)
            {
                if (int.TryParse(cmdArgs[cmdArgs.Length - 1], out int powerToysRunnerPid))
                {
                    var dispatcher = DispatcherQueue.GetForCurrentThread();
                    RunnerHelper.WaitForPowerToysRunner(powerToysRunnerPid, () =>
                    {
                        dispatcher.TryEnqueue(App.Current.Exit);
                    });
                }
            }

            PowerToys.MeasureToolCore.Core core = null;
            try
            {
                core = new PowerToys.MeasureToolCore.Core();
            }
            catch (Exception ex)
            {
                Logger.LogError($"MeasureToolCore failed to initialize: {ex}");
                App.Current.Exit();
                return;
            }

            _window = new MainWindow(core);
            _window.Activate();
        }

        private Window _window;
    }
}
