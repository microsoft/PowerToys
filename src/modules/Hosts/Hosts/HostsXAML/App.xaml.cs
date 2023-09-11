// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO.Abstractions;
using System.Threading;
using Hosts.Helpers;
using Hosts.Settings;
using Hosts.ViewModels;
using Hosts.Views;
using ManagedCommon;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.PowerToys.Telemetry;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;

namespace Hosts
{
    public partial class App : Application
    {
        private Window _window;

        public IHost Host
        {
            get;
        }

        public static T GetService<T>()
            where T : class
        {
            if ((App.Current as App)!.Host.Services.GetService(typeof(T)) is not T service)
            {
                throw new ArgumentException($"{typeof(T)} needs to be registered in ConfigureServices within App.xaml.cs.");
            }

            return service;
        }

        public App()
        {
            InitializeComponent();

            Host = Microsoft.Extensions.Hosting.Host.
                CreateDefaultBuilder().
                UseContentRoot(AppContext.BaseDirectory).
                ConfigureServices((context, services) =>
                {
                    // Core Services
                    services.AddSingleton<IFileSystem, FileSystem>();
                    services.AddSingleton<IHostsService, HostsService>();
                    services.AddSingleton<IUserSettings, UserSettings>();
                    services.AddSingleton<IElevationHelper, ElevationHelper>();

                    // Views and ViewModels
                    services.AddTransient<MainPage>();
                    services.AddTransient<MainViewModel>();
                }).
                Build();

            UnhandledException += App_UnhandledException;

            var cleanupBackupThread = new Thread(() =>
            {
                // Delete old backups only if running elevated
                if (!GetService<IElevationHelper>().IsElevated)
                {
                    return;
                }

                try
                {
                    GetService<IHostsService>().CleanupBackup();
                }
                catch (Exception ex)
                {
                    Logger.LogError("Failed to delete backup", ex);
                }
            });

            cleanupBackupThread.IsBackground = true;
            cleanupBackupThread.Start();
        }

        protected override void OnLaunched(LaunchActivatedEventArgs args)
        {
            var cmdArgs = Environment.GetCommandLineArgs();
            if (cmdArgs?.Length > 1)
            {
                if (int.TryParse(cmdArgs[cmdArgs.Length - 1], out int powerToysRunnerPid))
                {
                    Logger.LogInfo($"Hosts started from the PowerToys Runner. Runner pid={powerToysRunnerPid}");

                    var dispatcher = DispatcherQueue.GetForCurrentThread();
                    RunnerHelper.WaitForPowerToysRunner(powerToysRunnerPid, () =>
                    {
                        Logger.LogInfo("PowerToys Runner exited. Exiting Hosts");
                        dispatcher.TryEnqueue(App.Current.Exit);
                    });
                }
            }
            else
            {
                Logger.LogInfo($"Hosts started detached from PowerToys Runner.");
            }

            PowerToysTelemetry.Log.WriteEvent(new Hosts.Telemetry.HostsFileEditorOpenedEvent());

            _window = new MainWindow();
            _window.Activate();
        }

        private void App_UnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
        {
            Logger.LogError("Unhandled exception", e.Exception);
        }
    }
}
