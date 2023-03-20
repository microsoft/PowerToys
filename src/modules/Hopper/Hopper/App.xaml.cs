// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;
using System.Windows;
using Hopper;

namespace PowerToys.Hopper
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private void Application_Startup(object sender, StartupEventArgs e)
        {
            HopperBatch files = HopperBatch.FromCommandLine(Console.In, e.Args);
            MainWindow wnd = new MainWindow(files.Files.ToArray());
            wnd.Show();
        }
    }
}
