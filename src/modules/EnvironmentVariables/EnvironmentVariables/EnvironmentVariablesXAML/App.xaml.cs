// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO.Abstractions;
using EnvironmentVariables.Telemetry;
using EnvironmentVariablesUILib;
using EnvironmentVariablesUILib.Helpers;
using EnvironmentVariablesUILib.Telemetry;
using EnvironmentVariablesUILib.ViewModels;
using ManagedCommon;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.PowerToys.Telemetry;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;

namespace EnvironmentVariables
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    public partial class App : Application
    {
        public IHost Host { get; }

        public static T GetService<T>()
            where T : class
        {
            if ((App.Current as App)!.Host.Services.GetService(typeof(T)) is not T service)
            {
                throw new ArgumentException($"{typeof(T)} needs to be registered in ConfigureServices within App.xaml.cs.");
            }

            return service;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="App"/> class.
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App()
        {
            this.InitializeComponent();

            Host = Microsoft.Extensions.Hosting.Host.CreateDefaultBuilder().UseContentRoot(AppContext.BaseDirectory).ConfigureServices((context, services) =>
            {
                services.AddSingleton<IFileSystem, FileSystem>();
                services.AddSingleton<IElevationHelper, ElevationHelper>();
                services.AddSingleton<IEnvironmentVariablesService, EnvironmentVariablesService>();
                services.AddSingleton<ILogger, LoggerWrapper>();
                services.AddSingleton<ITelemetry, TelemetryWrapper>();

                services.AddSingleton<MainViewModel>();
                services.AddSingleton<EnvironmentVariablesMainPage>();
            }).Build();

            UnhandledException += App_UnhandledException;
        }

        private void App_UnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
        {
            Logger.LogError("Unhandled exception", e.Exception);
        }

        /// <summary>
        /// Invoked when the application is launched.
        /// </summary>
        /// <param name="args">Details about the launch request and process.</param>
        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            var cmdArgs = Environment.GetCommandLineArgs();
            if (cmdArgs?.Length > 1)
            {
                if (int.TryParse(cmdArgs[cmdArgs.Length - 1], out int powerToysRunnerPid))
                {
                    Logger.LogInfo($"EnvironmentVariables started from the PowerToys Runner. Runner pid={powerToysRunnerPid}.");

                    var dispatcher = DispatcherQueue.GetForCurrentThread();
                    RunnerHelper.WaitForPowerToysRunner(powerToysRunnerPid, () =>
                    {
                        Logger.LogInfo("PowerToys Runner exited. Exiting EnvironmentVariables");
                        dispatcher.TryEnqueue(App.Current.Exit);
                    });
                }
            }
            else
            {
                Logger.LogInfo($"EnvironmentVariables started detached from PowerToys Runner.");
            }

            PowerToysTelemetry.Log.WriteEvent(new Telemetry.EnvironmentVariablesOpenedEvent());
            window = new MainWindow();
            window.Activate();
        }

        private Window window;
    }
}
