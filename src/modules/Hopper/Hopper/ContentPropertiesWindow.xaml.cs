// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Specialized;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Shell;
using Windows.Media.Audio;
using DataObject = System.Windows.DataObject;
using DragDropEffects = System.Windows.DragDropEffects;
using Path = System.IO.Path;
using RoutedEventArgs = System.Windows.RoutedEventArgs;

namespace Hopper
{
    public partial class ContentPropertiesWindow : System.Windows.Window
    {
        private readonly string[] _files;

        private bool _createFolderMode;

        public ContentPropertiesWindow(string[] files)
        {
            InitializeComponent();
            _files = files;
            DragFolder.AllowDrop = true;
            DragFolder.PreviewMouseLeftButtonDown += DragFolder_PreviewMouseLeftButtonDown;

            SetDefaultTitleToTitleTextBlock();
        }

        private void DragFolder_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            string tmpPath = Path.GetTempPath() + "PowerToys\\Hopper\\" + NewFolderName.Text;
            if (Directory.Exists(tmpPath))
            {
                Directory.Delete(tmpPath, true);
            }

            Directory.CreateDirectory(tmpPath);

            CreateNewFolder.NewFolderWithFiles(_files, tmpPath, true);
            new Task(() =>
            {
                Dispatcher.Invoke(() =>
                {
                    DataObject dataObject = new DataObject(System.Windows.DataFormats.FileDrop);
                    dataObject.SetFileDropList(new StringCollection { tmpPath });

                    DragDrop.DoDragDrop(DragFolder, dataObject, System.Windows.DragDropEffects.Move);
                });
            }).Start();
            SetFileWatcher(tmpPath);
        }

        private void SetFileWatcher(string path)
        {
            FileSystemWatcher watcher = new FileSystemWatcher();
            watcher.Path = path;
            watcher.NotifyFilter = NotifyFilters.LastAccess | NotifyFilters.LastWrite
               | NotifyFilters.FileName | NotifyFilters.DirectoryName;

            watcher.Deleted += (object sender, FileSystemEventArgs e) =>
            {
                Dispatcher.Invoke(() => Debugger.Launch());
                foreach (string file in _files)
                {
                    File.Delete(file);
                }

                Dispatcher.Invoke(() => Close());
            };
        }

        private void CreateFolderButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_createFolderMode)
            {
                var folderBrowserDialog = new FolderBrowserDialog();
                folderBrowserDialog.Description = Properties.Resources.Hopper_folder_chooser_description;
                folderBrowserDialog.ShowNewFolderButton = true;
                folderBrowserDialog.AutoUpgradeEnabled = true;
                folderBrowserDialog.UseDescriptionForTitle = true;

                if (folderBrowserDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    string folder = folderBrowserDialog.SelectedPath;
                    NewFolderName.Text = folder;
                    CreateFolderButton.Content = Properties.Resources.Hopper_create_folder;
                    _createFolderMode = true;
                }
            }
            else
            {
                CreateNewFolder.NewFolderWithFiles(_files, NewFolderName.Text);

                Close();
            }
        }

        private void NewFolderName_GotFocus(object sender, RoutedEventArgs e)
        {
            _createFolderMode = false;
            SetDefaultTitleToTitleTextBlock();
            NewFolderName.Text = Path.GetFileName(NewFolderName.Text);
            CreateFolderButton.Content = Properties.Resources.Hopper_select_folder;
        }

        private void SetDefaultTitleToTitleTextBlock()
        {
            TitleTextBlock.Text = _files?.Length != 1
                ? Properties.Resources.Hopper_files_to_new_folder.Replace("%1", _files?.Length.ToString(CultureInfo.InvariantCulture))
                : Properties.Resources.Hopper_file_to_new_folder;
        }

        private void DragFolder_DragEnter(object sender, System.Windows.DragEventArgs e)
        {
            e.Effects = System.Windows.DragDropEffects.Move;
        }
    }
}
