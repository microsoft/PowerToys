// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace Hopper
{
    public partial class MainWindow
    {
        public MainWindow(IEnumerable<string> files)
        {
            // Open debugger, so that this app can be debugged when launched from explorer context menu
#if DEBUG
            Debugger.Launch();
#endif

            string[] file2 = Array.Empty<string>();

            file2 = files.Where(static file => file.TrimStart() != "\0").Aggregate(file2, static (current, file) => current.Append(file.Replace("\0", string.Empty)).ToArray());

            InitializeComponent();
            FileList.ItemsSource = file2;
            FileList.ContextMenu = new ContextMenu();
            MenuItem menuItem = new MenuItem
            {
                Header = "Remove",
            };
            menuItem.Click += (_, _) =>
            {
                string[] newFiles = Array.Empty<string>();
                newFiles = FileList.ItemsSource.Cast<string>().Where(file => file != FileList.SelectedItem.ToString()).Aggregate(newFiles, static (current, file) => current.Append(file).ToArray());

                FileList.ItemsSource = newFiles;
                if (newFiles.Length == 0)
                {
                    CreateFolderButton.IsEnabled = false;
                }
            };
            FileList.ContextMenu.Items.Add(menuItem);
            ContextMenu contextMenu = FileList.ContextMenu;
            FileList.PreviewMouseRightButtonDown += (_, _) =>
            {
                FileList.ContextMenu = FileList.SelectedItem == null ? null : contextMenu;
            };

            if (file2.Length == 0)
            {
                CreateFolderButton.IsEnabled = false;
            }
        }

        private void Window_Drop(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                return;
            }

            CreateFolderButton.IsEnabled = true;

            // Note that you can have more than one file.
            string[] files = (string[])(e.Data.GetData(DataFormats.FileDrop) ?? Array.Empty<string>());

            string[] oldFiles = (string[])FileList.ItemsSource;

            foreach (var file in files.Where(file => !oldFiles.Contains(file)))
            {
                oldFiles = oldFiles.Append(file).ToArray();
            }

            FileList.ItemsSource = oldFiles;
            FileList.ScrollIntoView(FileList.Items[^1]);
        }

        private void CreateFolderButton_Click(object sender, RoutedEventArgs e)
        {
            Window con = new Step2Window((string[])FileList.ItemsSource);
            con.Show();
            Close();
        }

        private void ClearButton_OnClick(object sender, RoutedEventArgs e)
        {
            FileList.ItemsSource = Array.Empty<string>();
            CreateFolderButton.IsEnabled = false;
        }
    }
}
