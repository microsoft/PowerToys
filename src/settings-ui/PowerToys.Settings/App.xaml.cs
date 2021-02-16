// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Windows;

namespace PowerToys.Settings
{
    /// <summary>
    /// Interaction logic for App.xaml.
    /// </summary>
    public partial class App : Application
    {
        public bool ShowOobe { get; set; }

        private void Application_Startup(object sender, StartupEventArgs e)
        {
            if (!ShowOobe)
            {
                MainWindow mainWindow = new MainWindow();
                mainWindow.Show();
            }
            else
            {
                OobeWindow otherWindow = new OobeWindow();
                otherWindow.Show();
            }
        }
    }
}
