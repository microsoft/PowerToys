// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Interop;
using Microsoft.UI.Xaml;
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
            SetDefaultTitleToTitleTextBlock();
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
            CreateFolderButton.Content = "Select folder";
        }

        private void SetDefaultTitleToTitleTextBlock()
        {
            TitleTextBlock.Text = _files?.Length != 1
                ? Properties.Resources.Hopper_files_to_new_folder.Replace("%1", _files?.Length.ToString(CultureInfo.InvariantCulture))
                : Properties.Resources.Hopper_file_to_new_folder;
        }
    }
}
