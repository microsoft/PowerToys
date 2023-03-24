// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using Hopper;

namespace PowerToys.Hopper
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow(string[] files)
        {
            // Open debugger, so that this app can be debugged when launched from explorer context menu
#if DEBUG
            Debugger.Launch();
            Debugger.Break();
#endif

            string[] file2 = Array.Empty<string>();

            foreach (var file in files)
            {
                if (file.TrimStart() != "\0")
                {
                    file2 = file2.Append(file.Replace("\0", string.Empty)).ToArray<string>();
                }
            }

            InitializeComponent();
            FileList.ItemsSource = file2;
        }

        private void CreateFolderButton_Click(object sender, RoutedEventArgs e)
        {
            Window con = new ContentPropertiesWindow((string[])FileList.ItemsSource);
            con.Show();
            Close();
        }

        private void Window_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                // Note that you can have more than one file.
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);

                string[] oldFiles = (string[])FileList.ItemsSource;

                foreach (string file in files)
                {
                    oldFiles = oldFiles.Append(file).ToArray<string>();
                }

                FileList.ItemsSource = oldFiles;
                FileList.ScrollIntoView(FileList.Items[FileList.Items.Count - 1]);
            }
        }
    }
}
