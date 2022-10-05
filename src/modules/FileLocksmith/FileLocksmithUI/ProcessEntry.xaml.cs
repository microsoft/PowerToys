// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Threading;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Automation.Provider;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using PowerToys.FileLocksmithUI.Properties;
using Windows.Graphics;
using WinUIEx;

namespace FileLocksmithUI
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class ProcessEntry : UserControl
    {
        public uint Pid { get; private set; }

        public ProcessEntry(string process, uint pid, ulong numFiles)
        {
            Pid = pid;
            InitializeComponent();
            processName.Text = process;

            processPid.Text = PowerToys.FileLocksmithUI.Properties.Resources.ProcessId + ": " + pid;
            processFileCount.Text = PowerToys.FileLocksmithUI.Properties.Resources.FilesUsed + ": " + numFiles;
            processUser.Text = PowerToys.FileLocksmithUI.Properties.Resources.User + ": " + "<TODO PID TO USER>";
        }

        public void AddPath(string path)
        {
            var entry = new TextBlock();
            entry.IsTextSelectionEnabled = true;
            entry.Text = path;
            entry.HorizontalAlignment = HorizontalAlignment.Left;

            filesContainer.Children.Add(entry);
        }

        private void KillProcessClick(object sender, RoutedEventArgs e)
        {
            if (!FileLocksmith.Interop.NativeMethods.KillProcess(Pid))
            {
                // TODO show something on failure.
            }
        }
    }
}
