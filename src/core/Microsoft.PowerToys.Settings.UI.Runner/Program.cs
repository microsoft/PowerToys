using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using TwoWayIPCLibLib;

namespace Microsoft.PowerToys.Settings.UI.Runner
{
    public class Program
    {
        // Create an instance of the  IPC wrapper 
        public static ITwoWayIPCManager ipcmanager = new TwoWayIPCManager();
        [System.STAThreadAttribute()]
        public static void Main(string[] args)
        {
            MessageBox.Show(
                        string.Join(",", args),
                        "Input",
                        MessageBoxButton.OK);
            using (new UI.App())
            {
                App app = new App();
                app.InitializeComponent();

                if (args.Length > 1)
                {
                    ipcmanager.Initialize(args[1], args[0]);
                    app.Run();
                    //ipcmanager.SendMessage("{\"general\":{\"startup\":true,\"run_elevated\":true,\"enabled\":{\"FancyZones\":true,\"PowerRename\":true,\"Shortcut Guide\":true}}}");
                }
                else
                {
                    MessageBox.Show(
                        "The application cannot be run as a standalone process. Please start the application through the runner.",
                        "Forbidden",
                        MessageBoxButton.OK);
                    app.Shutdown();
                }
            }
        }
    }
}