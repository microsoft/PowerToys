using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using Microsoft.VisualBasic.ApplicationServices;
using Wox.Commands;
using Wox.Helper;
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

            //for install plugin command when wox didn't start up
            //we shouldn't init MainWindow, just intall plugin and exit.
            if (e.Args.Length > 0 && e.Args[0].ToLower() == "installplugin")
            {
                var path = e.Args[1];
                if (!File.Exists(path))
                {
                    MessageBox.Show("Plugin " + path + " didn't exist");
                    return;
                }
                PluginInstaller.Install(path);
                Environment.Exit(0);
                return;
            }

            if (e.Args.Length > 0 && e.Args[0].ToLower() == "plugindebugger")
            {
                var path = e.Args[1];
                PluginLoader.Plugins.ActivatePluginDebugger(path);
            }

            window = new MainWindow();
            if (e.Args.Length == 0 || e.Args[0].ToLower() != "hidestart")
            {
                window.ShowApp();
            }

            window.ParseArgs(e.Args);
        }

        public void Activate(string[] args)
        {
            if (args.Length == 0 || args[0].ToLower() != "hidestart")
            {
                window.ShowApp();
            }
            window.ParseArgs(args);
        }
    }
}
