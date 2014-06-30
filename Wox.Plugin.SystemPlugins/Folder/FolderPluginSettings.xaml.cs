using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Wox.Infrastructure.Storage.UserSettings;

namespace Wox.Plugin.SystemPlugins.Folder {

	/// <summary>
	/// Interaction logic for FileSystemSettings.xaml
	/// </summary>
	public partial class FileSystemSettings : UserControl {
		public FileSystemSettings() {
			InitializeComponent();
			lbxFolders.ItemsSource = UserSettingStorage.Instance.FolderLinks;
		}

		private void btnDelete_Click(object sender, RoutedEventArgs e) {
            var selectedFolder = lbxFolders.SelectedItem as FolderLink;
            if (selectedFolder != null)
            {
                UserSettingStorage.Instance.FolderLinks.Remove(selectedFolder);
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

                var folderBrowserDialog = new System.Windows.Forms.FolderBrowserDialog();
                folderBrowserDialog.SelectedPath = selectedFolder.Path;
                if (folderBrowserDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    var link = UserSettingStorage.Instance.FolderLinks.First(x => x.Path == selectedFolder.Path);
                    link.Path = folderBrowserDialog.SelectedPath;

                    UserSettingStorage.Instance.Save();
                }

                lbxFolders.Items.Refresh();
            }
            else
            {
                MessageBox.Show("Please select a folder link!");
            }
		}

		private void btnAdd_Click(object sender, RoutedEventArgs e) {
			var folderBrowserDialog = new System.Windows.Forms.FolderBrowserDialog();
			if (folderBrowserDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK) {
				var newFolder = new FolderLink() {
					Path = folderBrowserDialog.SelectedPath
				};

				if (UserSettingStorage.Instance.FolderLinks == null) {
					UserSettingStorage.Instance.FolderLinks = new List<FolderLink>();
				}

				UserSettingStorage.Instance.FolderLinks.Add(newFolder);
				UserSettingStorage.Instance.Save();
			}

			lbxFolders.Items.Refresh();
		}
	}
}
