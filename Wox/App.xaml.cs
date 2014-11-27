using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Wox.CommandArgs;
using Wox.Helper;
using Wox.Helper.ErrorReporting;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;
using StartupEventArgs = System.Windows.StartupEventArgs;

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
            base.OnStartup(e);
            DispatcherUnhandledException += ErrorReporting.DispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += ErrorReporting.UnhandledExceptionHandle;
            System.Windows.Forms.Application.ThreadException += ErrorReporting.ThreadException;

            Window = new MainWindow();
            CommandArgsFactory.Execute(e.Args.ToList());
        }

        public bool OnActivate(IList<string> args)
        {
            CommandArgsFactory.Execute(args);
            return true;
        }
    }
}
