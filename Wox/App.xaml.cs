using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Wox.CommandArgs;
using Wox.Core.Plugin;
using Wox.Helper;
using Wox.Infrastructure.Image;
using Wox.ViewModel;
using Stopwatch = Wox.Infrastructure.Stopwatch;

namespace Wox
{
    public partial class App : ISingleInstanceApp
    {
        private const string Unique = "Wox_Unique_Application_Mutex";
        public static MainWindow Window { get; private set; }
        public static PublicAPIInstance API { get; private set; }
        private bool _saved;

        [STAThread]
        public static void Main()
        {
            if (SingleInstance<App>.InitializeAsFirstInstance(Unique))
            {
                var application = new App();
                application.InitializeComponent();
                application.Run();
                SingleInstance<App>.Cleanup();
            }
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            Stopwatch.Debug("Startup Time", () =>
            {
                base.OnStartup(e);
                RegisterUnhandledException();

                ImageLoader.PreloadImages();

                MainViewModel mainVM = new MainViewModel();
                var pluginsSettings = mainVM._settings.PluginSettings;
                API = new PublicAPIInstance(mainVM, mainVM._settings);
                PluginManager.InitializePlugins(API, pluginsSettings);

                Window = new MainWindow(mainVM._settings, mainVM);
                NotifyIconManager notifyIconManager = new NotifyIconManager(API);
                CommandArgsFactory.Execute(e.Args.ToList());
            });
        }

        [Conditional("RELEASE")]
        private void RegisterUnhandledException()
        {
            // let exception throw as normal is better for Debug
            DispatcherUnhandledException += ErrorReporting.DispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += ErrorReporting.UnhandledExceptionHandle;
        }

        public void OnActivate(IList<string> args)
        {
            if (args.Count > 0 && args[0] == SingleInstance<App>.Restart)
            {
                API.CloseApp();
            }
            else
            {
                CommandArgsFactory.Execute(args);
            }
        }

        private void OnExit(object sender, ExitEventArgs e)
        {
            Save();
        }

        private void OnSessionEnding(object sender, SessionEndingCancelEventArgs e)
        {
            Save();
        }

        private void Save()
        {
            // if sessionending is called, exit proverbially be called when log off / shutdown
            // but if sessionending is not called, exit won't be called when log off / shutdown
            if (!_saved)
            {
                var vm = (MainViewModel) Window.DataContext;
                vm.Save();
                PluginManager.Save();
                ImageLoader.Save();
                _saved = true;
            }
        }
    }
}
