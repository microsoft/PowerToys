// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Windows;
using ManagedCommon;
using Microsoft.PowerLauncher.Telemetry;
using Microsoft.PowerToys.Telemetry;
using PowerLauncher.Helper;
using PowerLauncher.ViewModel;
using Wox;
using Wox.Core.Plugin;
using Wox.Core.Resource;
using Wox.Infrastructure;
using Wox.Infrastructure.Http;
using Wox.Infrastructure.Image;
using Wox.Infrastructure.Logger;
using Wox.Infrastructure.UserSettings;
using Wox.Plugin;
using Stopwatch = Wox.Infrastructure.Stopwatch;

namespace PowerLauncher
{
    public partial class App : IDisposable, ISingleInstanceApp
    {
        public static PublicAPIInstance API { get; private set; }

        private readonly Alphabet _alphabet = new Alphabet();

        private const string Unique = "PowerLauncher_Unique_Application_Mutex";
        private static bool _disposed = false;
        private static int _powerToysPid;
        private Settings _settings;
        private MainViewModel _mainVM;
        private MainWindow _mainWindow;
        private ThemeManager _themeManager;
        private SettingWindowViewModel _settingsVM;
        private StringMatcher _stringMatcher;
        private SettingsWatcher _settingsWatcher;

        [STAThread]
        public static void Main(string[] args)
        {
            if (SingleInstance<App>.InitializeAsFirstInstance(Unique))
            {
                if (args?.Length > 0)
                {
                    _ = int.TryParse(args[0], out _powerToysPid);
                }

                using (var application = new App())
                {
                    application.InitializeComponent();
                    application.Run();
                }
            }
        }

        private void OnStartup(object sender, StartupEventArgs e)
        {
            RunnerHelper.WaitForPowerToysRunner(_powerToysPid, () =>
            {
                try
                {
                    Dispose();
                }
                finally
                {
                    Environment.Exit(0);
                }
            });

            var bootTime = new System.Diagnostics.Stopwatch();
            bootTime.Start();
            Stopwatch.Normal("|App.OnStartup|Startup cost", () =>
            {
                Log.Info("|App.OnStartup|Begin PowerToys Run startup ----------------------------------------------------");
                Log.Info($"|App.OnStartup|Runtime info:{ErrorReporting.RuntimeInfo()}");
                RegisterAppDomainExceptions();
                RegisterDispatcherUnhandledException();

                _themeManager = new ThemeManager(this);
                ImageLoader.Initialize(_themeManager.GetCurrentTheme());

                _settingsVM = new SettingWindowViewModel();
                _settings = _settingsVM.Settings;

                _alphabet.Initialize(_settings);
                _stringMatcher = new StringMatcher(_alphabet);
                StringMatcher.Instance = _stringMatcher;
                _stringMatcher.UserSettingSearchPrecision = _settings.QuerySearchPrecision;

                PluginManager.LoadPlugins(_settings.PluginSettings);
                _mainVM = new MainViewModel(_settings);
                _mainWindow = new MainWindow(_settings, _mainVM);
                API = new PublicAPIInstance(_settingsVM, _mainVM, _alphabet, _themeManager);
                PluginManager.InitializePlugins(API);

                Current.MainWindow = _mainWindow;
                Current.MainWindow.Title = Constant.ExeFileName;

                // happlebao todo temp fix for instance code logic
                // load plugin before change language, because plugin language also needs be changed
                InternationalizationManager.Instance.Settings = _settings;
                InternationalizationManager.Instance.ChangeLanguage(_settings.Language);

                // main windows needs initialized before theme change because of blur settings
                Http.Proxy = _settings.Proxy;

                RegisterExitEvents();

                _settingsWatcher = new SettingsWatcher(_settings);

                _mainVM.MainWindowVisibility = Visibility.Visible;
                _mainVM.ColdStartFix();
                _themeManager.ThemeChanged += OnThemeChanged;
                Log.Info("|App.OnStartup|End PowerToys Run startup ----------------------------------------------------  ");

                bootTime.Stop();

                PowerToysTelemetry.Log.WriteEvent(new LauncherBootEvent() { BootTimeMs = bootTime.ElapsedMilliseconds });

                // [Conditional("RELEASE")]
                // check update every 5 hours

                // check updates on startup
            });
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
                Stopwatch.Normal("|App.OnExit|Exit cost", () =>
                {
                    Log.Info("|App.OnExit| Start PowerToys Run Exit----------------------------------------------------  ");
                    if (disposing)
                    {
                        _themeManager.ThemeChanged -= OnThemeChanged;
                        API.SaveAppAllSettings();
                        PluginManager.Dispose();
                        _mainWindow.Dispose();
                        API.Dispose();
                        _mainVM.Dispose();
                        _themeManager.Dispose();
                        _disposed = true;
                    }

                    // TODO: free unmanaged resources (unmanaged objects) and override finalizer
                    // TODO: set large fields to null
                    _disposed = true;
                    Log.Info("|App.OnExit| End PowerToys Run Exit ----------------------------------------------------  ");
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
