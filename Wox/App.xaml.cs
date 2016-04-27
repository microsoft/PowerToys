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
    public partial class App : Application, ISingleInstanceApp
    {
        private const string Unique = "Wox_Unique_Application_Mutex";
        public static MainWindow Window { get; private set; }
        public static PublicAPIInstance API { get; private set; }

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

                Task.Factory.StartNew(ImageLoader.PreloadImages);

                MainViewModel mainVM = new MainViewModel();
                API = new PublicAPIInstance(mainVM, mainVM._settings);
                PluginManager.InitializePlugins(API);

                mainVM._settings.UpdatePluginSettings();

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
    }
}
