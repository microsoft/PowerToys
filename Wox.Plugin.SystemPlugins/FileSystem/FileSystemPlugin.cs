using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Wox.Infrastructure;
using Wox.Infrastructure.Storage.UserSettings;

namespace Wox.Plugin.SystemPlugins.FileSystem {

	public class FileSystemPlugin : BaseSystemPlugin, ISettingProvider {

		#region Properties

		private PluginInitContext context;
		private static List<string> driverNames = null;
		private static Dictionary<string, DirectoryInfo[]> parentDirectories = new Dictionary<string, DirectoryInfo[]>();
		public override string Description { get { return base.Description; } }
		public override string Name { get { return "File System"; } }
		public override string IcoPath { get { return @"Images\folder.png"; } }

		#endregion Properties

		#region Misc

		protected override void InitInternal(PluginInitContext context) {
			this.context = context;

			if (UserSettingStorage.Instance.FolderLinks == null) {
				UserSettingStorage.Instance.FolderLinks = new List<FolderLink>();
				UserSettingStorage.Instance.Save();
			}
		}

		public System.Windows.Controls.Control CreateSettingPanel() {
			return new FileSystemSettings();
		}

		#endregion Misc

		protected override List<Result> QueryInternal(Query query) {
			var results = new List<Result>();
			var input = query.RawQuery.ToLower();
			var inputName = input.Split(new string[] { @"\" }, StringSplitOptions.None).First().ToLower();
			var link = UserSettingStorage.Instance.FolderLinks.FirstOrDefault(x => x.Nickname.Equals(inputName, StringComparison.OrdinalIgnoreCase));
			var currentPath = link == null ? input : link.Path + input.Remove(0, inputName.Length);
			InitialDriverList();

			foreach (var item in UserSettingStorage.Instance.FolderLinks.Where(x => x.Nickname.StartsWith(input, StringComparison.OrdinalIgnoreCase))) {
				results.Add(new Result(item.Nickname, "Images/folder.png") {
					Action = (c) => {
						context.ChangeQuery(item.Nickname);
						return false;
					}
				});
			}

			if (link == null && !driverNames.Any(input.StartsWith))
				return results;

			QueryInternal_Directory_Exists(currentPath, input, results);

			return results;
		}

		private void InitialDriverList() {
			if (driverNames == null) {
				driverNames = new List<string>();
				var allDrives = DriveInfo.GetDrives();
				foreach (var driver in allDrives) {
					driverNames.Add(driver.Name.ToLower().TrimEnd('\\'));
				}
			}
		}

		private void QueryInternal_Directory_Exists(string currentPath, string input, List<Result> results) {
			string path = Directory.Exists(currentPath) ? new DirectoryInfo(currentPath).FullName : Path.GetDirectoryName(input);
			if (!System.IO.Directory.Exists(path)) return;

			results.Add(new Result("Open this directory", "Images/folder.png") {
				Score = 100000,
				Action = (c) => {
					if (Directory.Exists(currentPath)) {
						Process.Start(currentPath);
					}
					else if (currentPath.Contains("\\")) {
						var index = currentPath.LastIndexOf("\\");
						Process.Start(currentPath.Remove(index) + "\\");
					}

					return true;
				}
			});


			//if (System.IO.Directory.Exists(input)) {
			var dirs = new DirectoryInfo(path).GetDirectories();

			var parentDirKey = input.TrimEnd('\\', '/');
			if (!parentDirectories.ContainsKey(parentDirKey)) parentDirectories.Add(parentDirKey, dirs);

			var fuzzy = FuzzyMatcher.Create(Path.GetFileName(currentPath).ToLower());
			foreach (var dir in dirs) {				//.Where(x => (x.Attributes & FileAttributes.Hidden) != 0)) {
				if ((dir.Attributes & FileAttributes.Hidden) == FileAttributes.Hidden) continue;

				var result = new Result(dir.Name, "Images/folder.png") {
					Action = (c) => {
						//context.ChangeQuery(dir.FullName);
						context.ChangeQuery(input + dir.Name + "\\");
						return false;
					}
				};

				if (Path.GetFileName(currentPath).ToLower() != "") {
					var matchResult = fuzzy.Evaluate(dir.Name);
					result.Score = matchResult.Score;
					if (!matchResult.Success) continue;
				}

				results.Add(result);
			}
			//}

			var Folder = Path.GetDirectoryName(currentPath);
			if (Folder != null) {

				//var fuzzy = FuzzyMatcher.Create(Path.GetFileName(currentPath).ToLower());
				foreach (var dir in new DirectoryInfo(Folder).GetFiles()) { //.Where(x => (x.Attributes & FileAttributes.Hidden) != 0)) {
					if ((dir.Attributes & FileAttributes.Hidden) == FileAttributes.Hidden) continue;

					var dirPath = dir.FullName;
					Result result = new Result(Path.GetFileNameWithoutExtension(dirPath), dirPath) {
						Action = (c) => {
							try {
								Process.Start(dirPath);
							}
							catch (Exception ex) {
								MessageBox.Show(ex.Message, "Could not start " + dir.Name);
							}

							return true;
						}
					};

					if (Path.GetFileName(currentPath) != "") {
						var matchResult = fuzzy.Evaluate(dir.Name);
						result.Score = matchResult.Score;
						if (!matchResult.Success) continue;
					}

					results.Add(result);
				}
			}
		}
	}
}