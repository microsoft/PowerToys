// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Windows;
using Microsoft.PowerToys.Settings.UI.Views;
using System.Threading;

namespace Microsoft.PowerToys.Settings.UI.Runner
{
    // Interaction logic for MainWindow.xaml.
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            this.InitializeComponent();
        }

        private void WindowsXamlHost_ChildChanged(object sender, EventArgs e)
        {
            // Hook up x:Bind source.
            WindowsXamlHost windowsXamlHost = sender as WindowsXamlHost;
            ShellPage shellPage = windowsXamlHost.GetUwpInternalObject() as ShellPage;

            if (shellPage != null)
            {
                // send IPC Message
                shellPage.SetDefaultSndMessageCallback(delegate (string msg)
                {
                    Program.ipcmanager.SendMessage(msg);
                });

            }
        }

            if (shellPage != null)
            {
                // send IPC Message
                shellPage.SetDefaultSndMessageCallback(msg =>
                {
                    Program.GetTwoWayIPCManager().SendMessage(msg);
                });
            }
        }
    }
}