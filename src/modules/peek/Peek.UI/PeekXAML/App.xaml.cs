// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using interop;
using ManagedCommon;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.PowerToys.Telemetry;
using Microsoft.UI.Xaml;
using Peek.Common;
using Peek.FilePreviewer;
using Peek.FilePreviewer.Models;
using Peek.UI.Native;
using Peek.UI.Telemetry.Events;
using Peek.UI.Views;

namespace Peek.UI
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    public partial class App : Application, IApp
    {
        public static int PowerToysPID { get; set; }

        public IHost Host
        {
            get;
        }

        private MainWindow? Window { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="App"/> class.
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App()
        {
            InitializeComponent();
            Logger.InitializeLogger("\\Peek\\Logs");

            Host = Microsoft.Extensions.Hosting.Host.
            CreateDefaultBuilder().
            UseContentRoot(AppContext.BaseDirectory).
            ConfigureServices((context, services) =>
            {
                // Core Services
                services.AddTransient<NeighboringItemsQuery>();
                services.AddSingleton<IUserSettings, UserSettings>();
                services.AddSingleton<IPreviewSettings, PreviewSettings>();

                // Views and ViewModels
                services.AddTransient<TitleBar>();
                services.AddTransient<FilePreview>();
                services.AddTransient<MainWindowViewModel>();
            }).
            Build();

            UnhandledException += App_UnhandledException;
        }

        public T GetService<T>()
            where T : class
        {
            if ((App.Current as App)!.Host.Services.GetService(typeof(T)) is not T service)
            {
                throw new ArgumentException($"{typeof(T)} needs to be registered in ConfigureServices within App.xaml.cs.");
            }

            return service;
        }

        /// <summary>
        /// Invoked when the application is launched.
        /// </summary>
        /// <param name="args">Details about the launch request and process.</param>
        protected override void OnLaunched(LaunchActivatedEventArgs args)
        {
            if (PowerToys.GPOWrapper.GPOWrapper.GetConfiguredPeekEnabledValue() == PowerToys.GPOWrapper.GpoRuleConfigured.Disabled)
            {
                Logger.LogWarning("Tried to start with a GPO policy setting the utility to always be disabled. Please contact your systems administrator.");
                Environment.Exit(0); // Current.Exit won't work until there's a window opened.
                return;
            }

            var cmdArgs = Environment.GetCommandLineArgs();
            if (cmdArgs?.Length > 1)
            {
                if (int.TryParse(cmdArgs[^1], out int powerToysRunnerPid))
                {
                    RunnerHelper.WaitForPowerToysRunner(powerToysRunnerPid, () =>
                    {
                        Environment.Exit(0);
                    });
                }
            }

            NativeEventWaiter.WaitForEventLoop(Constants.ShowPeekEvent(), OnPeekHotkey);
        }

        private void App_UnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
        {
            PowerToysTelemetry.Log.WriteEvent(new ErrorEvent() { HResult = (Common.Models.HResult)e.Exception.HResult, Failure = ErrorEvent.FailureType.AppCrash });
        }

        /// <summary>
        /// Handle Peek hotkey
        /// </summary>
        private void OnPeekHotkey()
        {
            // Need to read the foreground HWND before activating Peek to avoid focus stealing
            // Foreground HWND must always be Explorer or Desktop
            var foregroundWindowHandle = Windows.Win32.PInvoke.GetForegroundWindow();

            bool firstActivation = false;

            if (Window == null)
            {
                firstActivation = true;
                Window = new MainWindow();
            }

            Window.Toggle(firstActivation, foregroundWindowHandle);
        }
    }
}
