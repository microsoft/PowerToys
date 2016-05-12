using System;
using System.Diagnostics;
using System.Windows;
using Wox.Core;
using Wox.Core.Plugin;
using Wox.Core.UserSettings;
using Wox.Helper;
using Wox.Infrastructure.Image;
using Wox.Infrastructure.Storage;
using Wox.ViewModel;
using Stopwatch = Wox.Infrastructure.Stopwatch;

namespace Wox
{
    public partial class App : IDisposable, ISingleInstanceApp
    {
        public static PublicAPIInstance API { get; private set; }
        private const string Unique = "Wox_Unique_Application_Mutex";
        private static bool _disposed;
        private Settings _settings;

        [STAThread]
        public static void Main()
        {
            RegisterAppDomainUnhandledException();
            if (SingleInstance<App>.InitializeAsFirstInstance(Unique))
            {
                using (var application = new App())
                {
                    application.InitializeComponent();
                    application.Run();
                }
            }
        }

        private void OnStartup(object sender, StartupEventArgs e)
        {
            Stopwatch.Debug("Startup Time", () =>
            {
                RegisterDispatcherUnhandledException();

                ImageLoader.PreloadImages();

                var storage = new JsonStrorage<Settings>();
                _settings = storage.Load();

                PluginManager.LoadPlugins(_settings.PluginSettings);
                var vm = new MainViewModel(_settings, storage);
                var pluginsSettings = _settings.PluginSettings;
                var window = new MainWindow(_settings, vm);
                API = new PublicAPIInstance(_settings, vm);
                PluginManager.InitializePlugins(API);

                RegisterExitEvents();

                Current.MainWindow = window;
                Current.MainWindow.Title = Infrastructure.Wox.Name;
                window.Show();
            });
        }

        private async void OnActivated(object sender, EventArgs e)
        {
            // todo happlebao add option in gui
            if (_settings.AutoUpdates)
            {
                Updater.UpdateApp();
            }
        }

        private void RegisterExitEvents()
        {
            AppDomain.CurrentDomain.ProcessExit += (s, e) => Dispose();
            Current.Exit += (s, e) => Dispose();
            Current.SessionEnding += (s, e) => Dispose();
        }
        [Conditional("RELEASE")]
        private void RegisterDispatcherUnhandledException()
        {
            // let exception throw as normal is better for Debug 
            DispatcherUnhandledException += ErrorReporting.DispatcherUnhandledException;
        }

        [Conditional("RELEASE")]
        private static void RegisterAppDomainUnhandledException()
        {
            // let exception throw as normal is better for Debug 
            AppDomain.CurrentDomain.UnhandledException += ErrorReporting.UnhandledExceptionHandle;
        }

        public void Dispose()
        {
            // if sessionending is called, exit proverbially be called when log off / shutdown
            // but if sessionending is not called, exit won't be called when log off / shutdown
            if (!_disposed)
            {
                ((MainViewModel)Current.MainWindow?.DataContext)?.Save();
                _disposed = true;
            }
        }

        public void OnSecondAppStarted()
        {
            API.ShowApp();
        }
    }
}