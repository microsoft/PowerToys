// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
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
    public sealed partial class MainWindow : WindowEx, IDisposable
    {
        public MainWindow()
        {
            // TODO Read paths from stdin
            InitializeComponent();
            StartFindingProcesses();
        }

        private void OnRefreshClick(object sender, RoutedEventArgs e)
        {
            StartFindingProcesses();
        }

        private void StartFindingProcesses()
        {
            Thread thread = new Thread(FindProcesses);
            thread.Start();
            DisplayProgressRing();
        }

        private void FindProcesses()
        {
            var result = FileLocksmith.Interop.NativeMethods.FindProcessesRecursive(new string[1] { "C:\\Users" });

            DispatcherQueue.TryEnqueue(() =>
            {
                stackPanel.Children.Clear();
                foreach (var item in result)
                {
                    stackPanel.Children.Add(new ProcessEntry(item.name, item.pid, (ulong)item.files.Length));

                    // Add files to item
                    // Launch a thread to erase this entry if the process exits
                }
            });
        }

        private void DisplayNoResultsIfEmpty()
        {
            if (stackPanel.Children.Count == 0)
            {
                var textBlock = new TextBlock();

                textBlock.Text = PowerToys.FileLocksmithUI.Properties.Resources.NoResults;
                textBlock.FontSize = 24;
                textBlock.HorizontalAlignment = HorizontalAlignment.Center;
                textBlock.VerticalAlignment = VerticalAlignment.Center;

                stackPanel.Children.Add(textBlock);
            }
        }

        private void DisplayProgressRing()
        {
            stackPanel.Children.Clear();

            var ring = new ProgressRing();
            ring.Width = 64;
            ring.Height = 64;
            ring.Margin = new Thickness(0, 16, 0, 0);
            ring.IsIndeterminate = true;

            stackPanel.Children.Add(ring);
        }

        public void Dispose()
        {
        }
    }
}
