using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Wox.Infrastructure.Storage.UserSettings;

namespace Wox.Plugin.SystemPlugins.Folder_Links {
	/// <summary>
	/// Interaction logic for FolderLinksSettings.xaml
	/// </summary>
	public partial class FolderLinksSettings : UserControl {
		public FolderLinksSettings() {
			InitializeComponent();
			lbxFolders.ItemsSource = Wox.Infrastructure.Storage.UserSettings.UserSettingStorage.Instance.FolderLinks;
		}

		private void btnDelete_Click(object sender, RoutedEventArgs e) {

		}

		private void btnEdit_Click(object sender, RoutedEventArgs e) {
			var SelectedFolder = lbxFolders.SelectedItem as FolderLink;

			var Folder = new System.Windows.Forms.FolderBrowserDialog();
			Folder.SelectedPath = SelectedFolder.Path;
			if (Folder.ShowDialog() == System.Windows.Forms.DialogResult.OK) {
				var Result = UserSettingStorage.Instance.FolderLinks.First(x => x.Path == SelectedFolder.Path);
				Result.Path = Folder.SelectedPath;

				UserSettingStorage.Instance.Save();
			}


			lbxFolders.Items.Refresh();
		}

		private void btnAdd_Click(object sender, RoutedEventArgs e) {
			var Folder = new System.Windows.Forms.FolderBrowserDialog();
			if (Folder.ShowDialog() == System.Windows.Forms.DialogResult.OK) {
				var New_Folder = new Wox.Infrastructure.Storage.UserSettings.FolderLink() {
					Path = Folder.SelectedPath
				};

				if (UserSettingStorage.Instance.FolderLinks == null) {
					UserSettingStorage.Instance.FolderLinks = new List<FolderLink>();
				}

				UserSettingStorage.Instance.FolderLinks.Add(New_Folder);
				UserSettingStorage.Instance.Save();
			}

			lbxFolders.Items.Refresh();
		}
	}
}
