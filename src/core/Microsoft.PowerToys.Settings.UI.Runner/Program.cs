// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Windows;
using interop;
using Windows.UI.Popups;

namespace Microsoft.PowerToys.Settings.UI.Runner
{
    public class Program
    {
        // Quantity of arguments
        private const int ArgumentsQty = 5;

        // Create an instance of the  IPC wrapper.
        private static TwoWayPipeMessageIPCManaged ipcmanager;

        public static bool IsElevated { get; set; }

        public static bool IsUserAnAdmin { get; set; }

        [STAThread]
        public static void Main(string[] args)
        {
            using (new UI.App())
            {
                App app = new App();
                app.InitializeComponent();

                if (args.Length >= ArgumentsQty)
                {
                    if (args[4] == "true")
                    {
                        IsElevated = true;
                    }
                    else
                    {
                        IsElevated = false;
                    }

                    if (args[5] == "true")
                    {
                        IsUserAnAdmin = true;
                    }
                    else
                    {
                        IsUserAnAdmin = false;
                    }

                    ipcmanager = new TwoWayPipeMessageIPCManaged(args[1], args[0], null);
                    ipcmanager.Start();
                    app.Run();
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

        public static TwoWayPipeMessageIPCManaged GetTwoWayIPCManager()
        {
            return ipcmanager;
        }
    }
}
