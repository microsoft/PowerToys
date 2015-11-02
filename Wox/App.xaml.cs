using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Wox.CommandArgs;
using Wox.Core.Plugin;
using Wox.Helper;
using Wox.Infrastructure;

namespace Wox
{
    public partial class App : Application, ISingleInstanceApp
    {
        private const string Unique = "Wox_Unique_Application_Mutex";
        public static MainWindow Window { get; private set; }

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
            using (new Timeit("Startup Time"))
            {
                base.OnStartup(e);
                DispatcherUnhandledException += ErrorReporting.DispatcherUnhandledException;
                AppDomain.CurrentDomain.UnhandledException += ErrorReporting.UnhandledExceptionHandle;

                Window = new MainWindow();
                PluginManager.Init(Window);
                ImageLoader.ImageLoader.PreloadImages();
                CommandArgsFactory.Execute(e.Args.ToList());
            }

        }

        public bool OnActivate(IList<string> args)
        {
            CommandArgsFactory.Execute(args);
            return true;
        }
    }
}
