using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Windows;
using Microsoft.VisualBasic.ApplicationServices;
using StartupEventArgs = System.Windows.StartupEventArgs;

namespace WinAlfred
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
        private MainWindow window;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            window = new MainWindow();
            window.ShowApp(e.Args);
        }

        public void Activate(string[] commandLine)
        {
            window.ShowApp(commandLine);
        }
    }
}
