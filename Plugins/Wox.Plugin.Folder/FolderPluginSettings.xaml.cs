using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Forms;
using MessageBox = System.Windows.MessageBox;
using UserControl = System.Windows.Controls.UserControl;

namespace Wox.Plugin.Folder
{

    /// <summary>
    /// Interaction logic for FileSystemSettings.xaml
    /// </summary>
    public partial class FileSystemSettings : UserControl
    {
        public FileSystemSettings()
        {
            InitializeComponent();
            lbxFolders.ItemsSource = FolderStorage.Instance.FolderLinks;
        }

        private void btnDelete_Click(object sender, RoutedEventArgs e)
        {
            var selectedFolder = lbxFolders.SelectedItem as FolderLink;
            if (selectedFolder != null)
            {
                FolderStorage.Instance.FolderLinks.Remove(selectedFolder);
                lbxFolders.Items.Refresh();
            }
            else
            {
                MessageBox.Show("Please select a folder link!");
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
                MessageBox.Show("Please select a folder link!");
            }
        }

        private void btnAdd_Click(object sender, RoutedEventArgs e)
        {
            var folderBrowserDialog = new System.Windows.Forms.FolderBrowserDialog();
            if (folderBrowserDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                var newFolder = new FolderLink()
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
    }
}
