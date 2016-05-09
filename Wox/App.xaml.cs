using System;
using System.Diagnostics;
using System.Net;
using System.Windows;
using Squirrel;
using Wox.Core.Plugin;
using Wox.Helper;
using Wox.Infrastructure.Image;
using Wox.ViewModel;
using Stopwatch = Wox.Infrastructure.Stopwatch;
using Wox.Infrastructure.Logger;

namespace Wox
{
    public partial class App : ISingleInstanceApp, IDisposable
    {
        private const string Unique = "Wox_Unique_Application_Mutex";
        public static MainWindow Window { get; private set; }
        public static PublicAPIInstance API { get; private set; }
        private static bool _disposed;
        public static UpdateManager Updater;

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

                MainViewModel mainVM = new MainViewModel();
                var pluginsSettings = mainVM._settings.PluginSettings;
                API = new PublicAPIInstance(mainVM, mainVM._settings);
                PluginManager.InitializePlugins(API, pluginsSettings);

                Window = new MainWindow(mainVM._settings, mainVM);
                var _notifyIconManager = new NotifyIconManager(API);

                RegisterExitEvents();
            });
        }

        private async void OnActivated(object sender, EventArgs e)
        {
            try
            {
                using (Updater = await UpdateManager.GitHubUpdateManager(Infrastructure.Wox.Github))
                {
                    await Updater.UpdateApp();
                }
            }
            catch (WebException ex)
            {
                Log.Error(ex);
            }
            catch (Exception exception)
            {
                const string info = "Update.exe not found, not a Squirrel-installed app?";
                if (exception.Message == info)
                {
                    Log.Warn(info);
                }
                else
                {
                    throw;
                }
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

        public void OnActivate()
        {
            API.ShowApp();
        }

        private static void Save()
        {
            var vm = (MainViewModel)Window.DataContext;
            vm.Save();
            PluginManager.Save();
            ImageLoader.Save();
            _disposed = true;
        }


        public void Dispose()
        {
            // if sessionending is called, exit proverbially be called when log off / shutdown
            // but if sessionending is not called, exit won't be called when log off / shutdown
            if (!_disposed)
            {
                Save();
                Updater?.Dispose();
                SingleInstance<App>.Cleanup();
            }
        }
    }
}