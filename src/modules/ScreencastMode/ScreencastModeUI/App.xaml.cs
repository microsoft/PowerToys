// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using ManagedCommon;
using Microsoft.UI.Xaml;
using PowerToys.Interop;

namespace ScreencastModeUI
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    public partial class App : Application
    {
        private Window? _window;

        public App()
        {
            InitializeComponent();
            Logger.InitializeLogger("\\ScreencastMode\\ScreencastModeUI\\Logs");
        }

        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            _window = new MainWindow();
            _window.Activate();

            // Exit when PowerToys Runner exits (pattern from Peek)
            var cmdArgs = Environment.GetCommandLineArgs();
            if (cmdArgs?.Length > 1 && int.TryParse(cmdArgs[^1], out int runnerPid))
            {
                RunnerHelper.WaitForPowerToysRunner(runnerPid, () => Environment.Exit(0));
            }
        }
    }
}
