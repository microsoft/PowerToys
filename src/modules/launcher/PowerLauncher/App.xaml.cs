// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows;

using Common.UI;

using interop;

using ManagedCommon;

using Microsoft.PowerLauncher.Telemetry;
using Microsoft.PowerToys.Telemetry;

using PowerLauncher.Helper;
using PowerLauncher.Plugin;
using PowerLauncher.ViewModel;

using Wox;
using Wox.Infrastructure;
using Wox.Infrastructure.Image;
using Wox.Infrastructure.UserSettings;
using Wox.Plugin;
using Wox.Plugin.Logger;

using Stopwatch = Wox.Infrastructure.Stopwatch;

namespace PowerLauncher
{
    public partial class App : IDisposable, ISingleInstanceApp
    {
        public static PublicAPIInstance API { get; private set; }

        private readonly Alphabet _alphabet = new Alphabet();

        public static CancellationTokenSource NativeThreadCTS { get; private set; }

        private static bool _disposed;

        private PowerToysRunSettings _settings;
        private MainViewModel _mainVM;
        private MainWindow _mainWindow;
        private ThemeManager _themeManager;
        private SettingWindowViewModel _settingsVM;
        private StringMatcher _stringMatcher;
        private SettingsReader _settingsReader;

        // To prevent two disposals running at the same time.
        private static readonly object _disposingLock = new object();

        [STAThread]
        public static void Main()
        {
            NativeThreadCTS = new CancellationTokenSource();

            Log.Info($"Starting PowerToys Run with PID={Environment.ProcessId}", typeof(App));
            if (PowerToys.GPOWrapperProjection.GPOWrapper.GetConfiguredPowerLauncherEnabledValue() == PowerToys.GPOWrapperProjection.GpoRuleConfigured.Disabled)
            {
                Log.Warn("Tried to start with a GPO policy setting the utility to always be disabled. Please contact your systems administrator.", typeof(App));
                return;
            }

            int powerToysPid = GetPowerToysPId();
            if (powerToysPid != 0)
            {
                // The process started from the PT Run module interface. One instance is handled there.
                Log.Info($"Runner pid={powerToysPid}", typeof(App));
                SingleInstance<App>.CreateInstanceMutex();
            }
            else
            {
                // If PT Run is started as standalone application check if there is already running instance
                if (!SingleInstance<App>.InitializeAsFirstInstance())
                {
                    Log.Warn("There is already running PowerToys Run instance. Exiting PowerToys Run", typeof(App));
                    return;
                }
            }

            using (var application = new App())
            {
                application.InitializeComponent();

                NativeEventWaiter.WaitForEventLoop(
                    Constants.RunExitEvent(),
                    () =>
                    {
                        Log.Warn("RunExitEvent was signaled. Exiting PowerToys", typeof(App));
                        ExitPowerToys(application);
                    },
                    Application.Current.Dispatcher,
                    NativeThreadCTS.Token);

                if (powerToysPid != 0)
                {
                    RunnerHelper.WaitForPowerToysRunner(powerToysPid, () =>
                    {
                        Log.Info($"Runner with pid={powerToysPid} exited. Exiting PowerToys Run", typeof(App));
                        ExitPowerToys(application);
                    });
                }

                application.Run();
            }
        }

        private void OnStartup(object sender, StartupEventArgs e)
        {
            Log.Info("On Startup.", GetType());

            // Fix for .net 3.1.19 making PowerToys Run not adapt to DPI changes.
            PowerLauncher.Helper.NativeMethods.SetProcessDPIAware();
            var bootTime = new System.Diagnostics.Stopwatch();
            bootTime.Start();
            Stopwatch.Normal("App.OnStartup - Startup cost", () =>
            {
                var textToLog = new StringBuilder();
                textToLog.AppendLine("Begin PowerToys Run startup ----------------------------------------------------");
                textToLog.AppendLine(CultureInfo.InvariantCulture, $"Runtime info:{ErrorReporting.RuntimeInfo()}");

                RegisterAppDomainExceptions();
                RegisterDispatcherUnhandledException();

                _themeManager = new ThemeManager(this);
                ImageLoader.Initialize(_themeManager.GetCurrentTheme());

                _settingsVM = new SettingWindowViewModel();
                _settings = _settingsVM.Settings;
                _settings.StartedFromPowerToysRunner = e.Args.Contains("--started-from-runner");

                _alphabet.Initialize(_settings);
                _stringMatcher = new StringMatcher(_alphabet);
                StringMatcher.Instance = _stringMatcher;
                _stringMatcher.UserSettingSearchPrecision = _settings.QuerySearchPrecision;

                _mainVM = new MainViewModel(_settings, NativeThreadCTS.Token);
                _mainWindow = new MainWindow(_settings, _mainVM, NativeThreadCTS.Token);
                API = new PublicAPIInstance(_settingsVM, _mainVM, _alphabet, _themeManager);
                _settingsReader = new SettingsReader(_settings, _themeManager);
                _settingsReader.ReadSettings();

                PluginManager.InitializePlugins(API);

                Current.MainWindow = _mainWindow;
                Current.MainWindow.Title = Constant.ExeFileName;

                RegisterExitEvents();

                _settingsReader.ReadSettingsOnChange();

                _mainVM.MainWindowVisibility = Visibility.Visible;
                _mainVM.ColdStartFix();
                _themeManager.ThemeChanged += OnThemeChanged;
                textToLog.AppendLine("End PowerToys Run startup ----------------------------------------------------  ");

                bootTime.Stop();

                Log.Info(textToLog.ToString(), GetType());
                PowerToysTelemetry.Log.WriteEvent(new LauncherBootEvent() { BootTimeMs = bootTime.ElapsedMilliseconds });

                // [Conditional("RELEASE")]
                // check update every 5 hours

                // check updates on startup
            });
        }

        private static void ExitPowerToys(App app)
        {
            SingleInstance<App>.SingleInstanceMutex.Close();

            app.Dispatcher.Invoke(() => app.Shutdown());
        }

        private static int GetPowerToysPId()
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i + 1 < args.Length; i++)
            {
                if (args[i] == "-powerToysPid")
                {
                    int powerToysPid;
                    if (int.TryParse(args[i + 1], out powerToysPid))
                    {
                        return powerToysPid;
                    }

                    break;
                }
            }

            return 0;
        }

        private void RegisterExitEvents()
        {
            AppDomain.CurrentDomain.ProcessExit += (s, e) =>
            {
                Log.Info("AppDomain.CurrentDomain.ProcessExit", GetType());
                Dispose();
            };

            Current.Exit += (s, e) =>
            {
                NativeThreadCTS.Cancel();
                Log.Info("Application.Current.Exit", GetType());
                Dispose();
            };

            Current.SessionEnding += (s, e) =>
            {
                Log.Info("Application.Current.SessionEnding", GetType());
                Dispose();
            };
        }

        /// <summary>
        /// Callback when windows theme is changed.
        /// </summary>
        /// <param name="oldTheme">Previous Theme</param>
        /// <param name="newTheme">Current Theme</param>
        private void OnThemeChanged(Theme oldTheme, Theme newTheme)
        {
            ImageLoader.UpdateIconPath(newTheme);
            _mainVM.Query();
        }

        /// <summary>
        /// let exception throw as normal is better for Debug
        /// </summary>
        [Conditional("RELEASE")]
        private void RegisterDispatcherUnhandledException()
        {
            DispatcherUnhandledException += ErrorReporting.DispatcherUnhandledException;
        }

        /// <summary>
        /// let exception throw as normal is better for Debug
        /// </summary>
        [Conditional("RELEASE")]
        private static void RegisterAppDomainExceptions()
        {
            AppDomain.CurrentDomain.UnhandledException += ErrorReporting.UnhandledExceptionHandle;
        }

        public void OnSecondAppStarted()
        {
            Current.MainWindow.Visibility = Visibility.Visible;
        }

        protected virtual void Dispose(bool disposing)
        {
            // Prevent two disposes at the same time.
            lock (_disposingLock)
            {
                if (!disposing)
                {
                    return;
                }

                if (_disposed)
                {
                    return;
                }

                _disposed = true;
            }

            Stopwatch.Normal("App.OnExit - Exit cost", () =>
            {
                Log.Info("Start PowerToys Run Exit----------------------------------------------------  ", GetType());
                if (disposing)
                {
                    if (_themeManager != null)
                    {
                        _themeManager.ThemeChanged -= OnThemeChanged;
                    }

                    API?.SaveAppAllSettings();
                    PluginManager.Dispose();

                    // Dispose needs to be called on the main Windows thread, since some resources owned by the thread need to be disposed.
                    _mainWindow?.Dispatcher.Invoke(Dispose);
                    API?.Dispose();
                    _mainVM?.Dispose();
                    _themeManager?.Dispose();
                }

                Log.Info("End PowerToys Run Exit ----------------------------------------------------  ", GetType());
            });
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
