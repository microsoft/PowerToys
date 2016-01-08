using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Forms;
using DataFormats = System.Windows.DataFormats;
using DragDropEffects = System.Windows.DragDropEffects;
using DragEventArgs = System.Windows.DragEventArgs;
using MessageBox = System.Windows.MessageBox;
using UserControl = System.Windows.Controls.UserControl;

namespace Wox.Plugin.Folder
{

    public partial class FileSystemSettings : UserControl
    {
        private IPublicAPI woxAPI;

        public FileSystemSettings(IPublicAPI woxAPI)
        {
            this.woxAPI = woxAPI;
            InitializeComponent();
            lbxFolders.ItemsSource = FolderStorage.Instance.FolderLinks;
        }

        private void btnDelete_Click(object sender, RoutedEventArgs e)
        {
            var selectedFolder = lbxFolders.SelectedItem as FolderLink;
            if (selectedFolder != null)
            {
                string msg = string.Format(woxAPI.GetTranslation("wox_plugin_folder_delete_folder_link"), selectedFolder.Path);

                if (MessageBox.Show(msg, string.Empty, MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                {
                    FolderStorage.Instance.FolderLinks.Remove(selectedFolder);
                    lbxFolders.Items.Refresh();
                    FolderStorage.Instance.Save();
                }
            }
            else
            {
                string warning = woxAPI.GetTranslation("wox_plugin_folder_select_folder_link_warning");
                MessageBox.Show(warning);
            }
        }

        private void btnEdit_Click(object sender, RoutedEventArgs e)
        {
            var selectedFolder = lbxFolders.SelectedItem as FolderLink;
            if (selectedFolder != null)
            {
                var folderBrowserDialog = new FolderBrowserDialog();
                folderBrowserDialog.SelectedPath = selectedFolder.Path;
                if (folderBrowserDialog.ShowDialog() == DialogResult.OK)
                {
                    var link = FolderStorage.Instance.FolderLinks.First(x => x.Path == selectedFolder.Path);
                    link.Path = folderBrowserDialog.SelectedPath;

                    FolderStorage.Instance.Save();
                }

                lbxFolders.Items.Refresh();
            }
            else
            {
                string warning = woxAPI.GetTranslation("wox_plugin_folder_select_folder_link_warning");
                MessageBox.Show(warning);
            }
        }

        private void btnAdd_Click(object sender, RoutedEventArgs e)
        {
            var folderBrowserDialog = new FolderBrowserDialog();
            if (folderBrowserDialog.ShowDialog() == DialogResult.OK)
            {
                var newFolder = new FolderLink
                {
                    Path = folderBrowserDialog.SelectedPath
                };

                if (FolderStorage.Instance.FolderLinks == null)
                {
                    FolderStorage.Instance.FolderLinks = new List<FolderLink>();
                }

                FolderStorage.Instance.FolderLinks.Add(newFolder);
                FolderStorage.Instance.Save();
            }

            lbxFolders.Items.Refresh();
        }

        private void lbxFolders_Drop(object sender, DragEventArgs e)
        {
            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);

            if (files != null && files.Count() > 0)
            {
                if (FolderStorage.Instance.FolderLinks == null)
                {
                    FolderStorage.Instance.FolderLinks = new List<FolderLink>();
                }

                foreach (string s in files)
                {
                    if (Directory.Exists(s))
                    {
                        var newFolder = new FolderLink
                        {
                            Path = s
                        };

                        FolderStorage.Instance.FolderLinks.Add(newFolder);
                        FolderStorage.Instance.Save();
                    }

                    lbxFolders.Items.Refresh();
                }
            }
        }

        private void lbxFolders_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.Link;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
        }
    }
}
