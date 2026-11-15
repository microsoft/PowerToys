// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using ManagedCommon;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.PowerToys.Telemetry;
using Microsoft.UI.Xaml;
using Peek.Common;
using Peek.Common.Helpers;
using Peek.FilePreviewer;
using Peek.FilePreviewer.Models;
using Peek.FilePreviewer.Previewers;
using Peek.UI.Models;
using Peek.UI.Native;
using Peek.UI.Telemetry.Events;
using Peek.UI.Views;
using PowerToys.Interop;

using ClassificationMode = Peek.Common.Helpers.LaunchArgumentsClassifier.ClassificationMode;

namespace Peek.UI
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    public partial class App : Application, IApp, IDisposable
    {
        public static int PowerToysPID { get; set; }

        public ETWTrace EtwTrace { get; private set; } = new ETWTrace();

        public IHost Host
        {
            get;
        }

        private MainWindow? Window { get; set; }

        private const string RunnerProcessName = "PowerToys";
        private const int CliInvalidArgumentsExitCode = 2;

        private bool _disposed;
        private SelectedItem? _selectedItem;
        private bool _launchedFromCli;

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

            InitializeComponent();
            Logger.InitializeLogger("\\Peek\\Logs");

            Host = Microsoft.Extensions.Hosting.Host
                .CreateDefaultBuilder()
                .UseContentRoot(AppContext.BaseDirectory)
                .ConfigureServices((context, services) =>
                {
                    // Core Services
                    services.AddTransient<NeighboringItemsQuery>();
                    services.AddSingleton<IUserSettings, UserSettings>();
                    services.AddSingleton<IPreviewSettings, PreviewSettings>();

                    // Views and ViewModels
                    services.AddTransient<TitleBar>();
                    services.AddTransient<FilePreview>();
                    services.AddTransient<MainWindowViewModel>();
                })
                .Build();

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
                string[] launchArgs = cmdArgs[1..];
                var classification = LaunchArgumentsClassifier.Classify(launchArgs);

                switch (classification.Mode)
                {
                    case ClassificationMode.Runner:
                        TryHandleRunnerLaunch(classification.RunnerPid);
                        break;

                    case ClassificationMode.Cli:
                        TryHandleCliLaunch(classification.CliArguments!);
                        return;

                    case ClassificationMode.InvalidRunnerArguments:
                        Logger.LogError("Peek: invalid runner arguments. Expected '--runner-pid <pid>'.");
                        Environment.Exit(CliInvalidArgumentsExitCode);
                        return;

                    case ClassificationMode.None:
                    default:
                        break;
                }
            }

            NativeEventWaiter.WaitForEventLoop(Constants.ShowPeekEvent(), OnShowPeek);
            NativeEventWaiter.WaitForEventLoop(Constants.TerminatePeekEvent(), () =>
            {
                ShellPreviewHandlerPreviewer.ReleaseHandlerFactories();
                EtwTrace?.Dispose();
                Environment.Exit(0);
            });
        }

        private void TryHandleRunnerLaunch(int powerToysRunnerPid)
        {
            if (!IsRunnerProcessAlive(powerToysRunnerPid))
            {
                Logger.LogError($"Runner launch provided a PID that is not active or is not PowerToys.exe: {powerToysRunnerPid}");
                Environment.Exit(CliInvalidArgumentsExitCode);
                return;
            }

            RunnerHelper.WaitForPowerToysRunner(powerToysRunnerPid, () =>
            {
                EtwTrace?.Dispose();
                Environment.Exit(0);
            });
        }

        private void TryHandleCliLaunch(IReadOnlyList<string> launchArgs)
        {
            var validPaths = new List<string>(launchArgs.Count);
            var invalidPaths = new List<string>();

            foreach (string arg in launchArgs)
            {
                if (TryResolveExistingPath(arg, out string? resolvedPath))
                {
                    validPaths.Add(resolvedPath!);
                }
                else
                {
                    invalidPaths.Add(arg);
                    Logger.LogError($"Command line argument is not a valid file or directory: {arg}");
                }
            }

            if (validPaths.Count == 0)
            {
                Logger.LogError("No valid file or directory paths were provided");
                Environment.Exit(CliInvalidArgumentsExitCode);
                return;
            }

            _selectedItem = validPaths.Count == 1
                ? new SelectedItemByPath(validPaths[0])
                : new SelectedItemsByPaths(validPaths);
            _launchedFromCli = true;
            OnShowPeek();
        }

        private static bool TryResolveExistingPath(string path, out string? resolvedPath)
        {
            resolvedPath = null;

            try
            {
                string fullPath = Path.GetFullPath(path);
                if (File.Exists(fullPath) || Directory.Exists(fullPath))
                {
                    resolvedPath = fullPath;
                    return true;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Could not resolve command line path argument '{path}'.", ex);
            }

            return false;
        }

        private static bool IsRunnerProcessAlive(int pid)
        {
            try
            {
                using Process process = Process.GetProcessById(pid);
                return !process.HasExited && string.Equals(process.ProcessName, RunnerProcessName, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        private void App_UnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
        {
            PowerToysTelemetry.Log.WriteEvent(new ErrorEvent() { HResult = (Common.Models.HResult)e.Exception.HResult, Failure = ErrorEvent.FailureType.AppCrash });
        }

        /// <summary>
        /// Handle Peek hotkey
        /// </summary>
        private void OnShowPeek()
        {
            // null means explorer, not null means CLI
            if (_selectedItem == null)
            {
                // Need to read the foreground HWND before activating Peek to avoid focus stealing
                // Foreground HWND must always be Explorer or Desktop
                var foregroundWindowHandle = Windows.Win32.PInvoke_PeekUI.GetForegroundWindow();
                _selectedItem = new SelectedItemByWindowHandle(foregroundWindowHandle);
            }

            bool firstActivation = false;

            if (Window == null)
            {
                firstActivation = true;
                Window = new MainWindow();
            }

            Window.Toggle(firstActivation, _selectedItem, _launchedFromCli);
            _launchedFromCli = false;
            _selectedItem = null;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    // dispose managed state (managed objects)
                }

                // free unmanaged resources (unmanaged objects) and override finalizer
                // set large fields to null
                _disposed = true;
            }
        }

        /* // override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
        // ~App()
        // {
        //     // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        //     Dispose(disposing: false);
        // } */

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
