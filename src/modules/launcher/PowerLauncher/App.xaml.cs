// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Windows;
using interop;
using ManagedCommon;
using Microsoft.PowerLauncher.Telemetry;
using Microsoft.PowerToys.Common.UI;
using Microsoft.PowerToys.Telemetry;
using PowerLauncher.Helper;
using PowerLauncher.Plugin;
using PowerLauncher.ViewModel;
using Wox;
using Wox.Infrastructure;
using Wox.Infrastructure.Http;
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

        private static bool _disposed;
        private PowerToysRunSettings _settings;
        private MainViewModel _mainVM;
        private MainWindow _mainWindow;
        private ThemeManager _themeManager;
        private SettingWindowViewModel _settingsVM;
        private StringMatcher _stringMatcher;
        private SettingsReader _settingsReader;

        [STAThread]
        public static void Main()
        {
            Log.Info($"Starting PowerToys Run with PID={Process.GetCurrentProcess().Id}", typeof(App));
            if (SingleInstance<App>.InitializeAsFirstInstance())
            {
                using (var application = new App())
                {
                    application.InitializeComponent();
                    NativeEventWaiter.WaitForEventLoop(Constants.RunExitEvent(), () =>
                    {
                        Log.Warn("RunExitEvent was signaled. Exiting PowerToys", typeof(App));
                        ExitPowerToys(application);
                    });

                    int powerToysPid = GetPowerToysPId();
                    if (powerToysPid != 0)
                    {
                        Log.Info($"Runner pid={powerToysPid}", typeof(App));
                        RunnerHelper.WaitForPowerToysRunner(powerToysPid, () =>
                        {
                            Log.Info($"Runner with pid={powerToysPid} exited. Exiting PowerToys Run", typeof(App));
                            ExitPowerToys(application);
                        });
                    }

                    application.Run();
                }
            }
            else
            {
                Log.Error("There is already running PowerToys Run instance. Exiting PowerToys Run", typeof(App));
            }
        }

        private void OnStartup(object sender, StartupEventArgs e)
        {
            Log.Info("On Startup.", GetType());
            var bootTime = new System.Diagnostics.Stopwatch();
            bootTime.Start();
            Stopwatch.Normal("App.OnStartup - Startup cost", () =>
            {
                var textToLog = new StringBuilder();
                textToLog.AppendLine("Begin PowerToys Run startup ----------------------------------------------------");
                textToLog.AppendLine($"Runtime info:{ErrorReporting.RuntimeInfo()}");

                RegisterAppDomainExceptions();
                RegisterDispatcherUnhandledException();

                _themeManager = new ThemeManager(this);
                ImageLoader.Initialize(_themeManager.GetCurrentTheme());

                _settingsVM = new SettingWindowViewModel();
                _settings = _settingsVM.Settings;
                _settings.UsePowerToysRunnerKeyboardHook = e.Args.Contains("--centralized-kb-hook");

                _stringMatcher = new StringMatcher();
                StringMatcher.Instance = _stringMatcher;
                _stringMatcher.UserSettingSearchPrecision = _settings.QuerySearchPrecision;

                _mainVM = new MainViewModel(_settings);
                _mainWindow = new MainWindow(_settings, _mainVM);
                API = new PublicAPIInstance(_settingsVM, _mainVM, _themeManager);
                _settingsReader = new SettingsReader(_settings, _themeManager);
                _settingsReader.ReadSettings();

                PluginManager.InitializePlugins(API);

                Current.MainWindow = _mainWindow;
                Current.MainWindow.Title = Constant.ExeFileName;

                // main windows needs initialized before theme change because of blur settings
                HttpClient.Proxy = _settings.Proxy;

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

            try
            {
                app.Dispose();
            }
            finally
            {
                Environment.Exit(0);
            }
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
            AppDomain.CurrentDomain.ProcessExit += (s, e) => Dispose();
            Current.Exit += (s, e) => Dispose();
            Current.SessionEnding += (s, e) => Dispose();
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
            if (!_disposed)
            {
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
                        _mainWindow?.Dispose();
                        API?.Dispose();
                        _mainVM?.Dispose();
                        _themeManager?.Dispose();
                        _disposed = true;
                    }

                    // TODO: free unmanaged resources (unmanaged objects) and override finalizer
                    // TODO: set large fields to null
                    _disposed = true;
                    Log.Info("End PowerToys Run Exit ----------------------------------------------------  ", GetType());
                });
            }
        }

        // // TODO: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
        // ~App()
        // {
        //     // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        //     Dispose(disposing: false);
        // }
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
