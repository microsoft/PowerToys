using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Windows;
using Microsoft.VisualBasic.ApplicationServices;
using Wox.Commands;
using StartupEventArgs = System.Windows.StartupEventArgs;

namespace Wox
{
    public static class EntryPoint
    {
        [STAThread]
        public static void Main(string[] args)
        {
            SingleInstanceManager manager = new SingleInstanceManager();
            manager.Run(args);
        }
    }

    // Using VB bits to detect single instances and process accordingly:
    //  * OnStartup is fired when the first instance loads
    //  * OnStartupNextInstance is fired when the application is re-run again
    //    NOTE: it is redirected to this instance thanks to IsSingleInstance
    public class SingleInstanceManager : WindowsFormsApplicationBase
    {
        App app;

        public SingleInstanceManager()
        {
            this.IsSingleInstance = true;
        }

        protected override bool OnStartup(Microsoft.VisualBasic.ApplicationServices.StartupEventArgs e)
        {
            // First time app is launched
            app = new App();
            app.InitializeComponent();
            app.Run();
            return true;
        }

        protected override void OnStartupNextInstance(StartupNextInstanceEventArgs eventArgs)
        {
            // Subsequent launches
            base.OnStartupNextInstance(eventArgs);
            app.Activate(eventArgs.CommandLine.ToArray());
        }
    }

    public partial class App : Application
    {

        private static MainWindow window;

        public static MainWindow Window
        {
            get
            {
                return window;
            }
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            window = new MainWindow();
            if (e.Args.Length == 0 || e.Args[0].ToLower() != "starthide")
            {
                window.ShowApp();
            }

            window.ParseArgs(e.Args);
        }

        public void Activate(string[] args)
        {
            if (args.Length == 0 || args[0].ToLower() != "starthide")
            {
                window.ShowApp();
            }
            window.ParseArgs(args);
        }
    }
}
