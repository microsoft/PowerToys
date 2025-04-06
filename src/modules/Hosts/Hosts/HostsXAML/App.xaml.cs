// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO.Abstractions;
using System.Threading;

using Common.UI;
using HostsEditor.Telemetry;
using HostsUILib.Helpers;
using HostsUILib.Settings;
using HostsUILib.ViewModels;
using HostsUILib.Views;
using ManagedCommon;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.PowerToys.Telemetry;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using PowerToys.Interop;
using static HostsUILib.Settings.IUserSettings;

using Host = Hosts.Helpers.Host;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.
namespace Hosts
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
            PowerToysTelemetry.Log.WriteEvent(new HostEditorStartEvent() { TimeStamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() });

            string appLanguage = LanguageHelper.LoadLanguage();
            if (!string.IsNullOrEmpty(appLanguage))
            {
                Microsoft.Windows.Globalization.ApplicationLanguages.PrimaryLanguageOverride = appLanguage;
            }

            InitializeComponent();

            Host.HostInstance = Microsoft.Extensions.Hosting.Host.
                CreateDefaultBuilder().
                UseContentRoot(AppContext.BaseDirectory).
                ConfigureServices((context, services) =>
                {
                    // Core Services
                    services.AddSingleton<IFileSystem, FileSystem>();
                    services.AddSingleton<IHostsService, HostsService>();
                    services.AddSingleton<IUserSettings, Hosts.Settings.UserSettings>();
                    services.AddSingleton<IElevationHelper, ElevationHelper>();
                    services.AddSingleton<IDuplicateService, DuplicateService>();

                    // Views and ViewModels
                    services.AddSingleton<ILogger, LoggerWrapper>();
                    services.AddSingleton<IElevationHelper, ElevationHelper>();
                    services.AddSingleton<OpenSettingsFunction>(() =>
                    {
                        SettingsDeepLink.OpenSettings(SettingsDeepLink.SettingsWindow.Hosts, true);
                    });

                    services.AddSingleton<MainViewModel, MainViewModel>();
                    services.AddSingleton<HostsMainPage, HostsMainPage>();
                }).
                Build();

            var cleanupBackupThread = new Thread(() =>
            {
                // Delete old backups only if running elevated
                if (!Host.GetService<IElevationHelper>().IsElevated)
                {
                    return;
                }

                try
                {
                    Host.GetService<IHostsService>().CleanupBackup();
                }
                catch (Exception ex)
                {
                    Logger.LogError("Failed to delete backup", ex);
                }
            });

            cleanupBackupThread.IsBackground = true;
            cleanupBackupThread.Start();

            UnhandledException += App_UnhandledException;

            Hosts.Helpers.NativeEventWaiter.WaitForEventLoop(Constants.TerminateHostsSharedEvent(), () =>
            {
                EtwTrace?.Dispose();
                Environment.Exit(0);
            });
        }

        /// <summary>
        /// Invoked when the application is launched.
        /// </summary>
        /// <param name="args">Details about the launch request and process.</param>
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
                        EtwTrace?.Dispose();
                        dispatcher.TryEnqueue(App.Current.Exit);
                    });
                }
            }
            else
            {
                Logger.LogInfo($"Hosts started detached from PowerToys Runner.");
            }

            PowerToysTelemetry.Log.WriteEvent(new Telemetry.HostsFileEditorOpenedEvent());

            window = new MainWindow();
            window.Activate();
        }

        private void App_UnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
        {
            Logger.LogError("Unhandled exception", e.Exception);
        }

        private Window window;

        public ETWTrace EtwTrace { get; private set; } = new ETWTrace();
    }
}
