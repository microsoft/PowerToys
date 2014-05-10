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

			#region 1

			//TODO: Consider always clearing the cache
			List<Result> results = new List<Result>();
			//if (string.IsNullOrEmpty(query.RawQuery)) {
			//	// clear the cache
			//	if (parentDirectories.Count > 0) parentDirectories.Clear();
			//	return results;
			//}

			#endregion 1

			var input = query.RawQuery.ToLower();
			var inputName = input.Split(new string[] { @"\" }, StringSplitOptions.None).First().ToLower();
			var link = UserSettingStorage.Instance.FolderLinks.FirstOrDefault(x => x.Nickname.Equals(inputName, StringComparison.OrdinalIgnoreCase));
			var currentPath = link == null ? input : link.Path + input.Remove(0, inputName.Length);

			InitialDriverList();

			foreach (var item in UserSettingStorage.Instance.FolderLinks.Where(x => x.Nickname.StartsWith(input, StringComparison.OrdinalIgnoreCase))) {
				//if (item.Nickname.StartsWith(input, StringComparison.OrdinalIgnoreCase)) { //&& item.Nickname.Length != input.Length) {
				results.Add(new Result(item.Nickname, "Images/folder.png") {
					Action = (c) => {
						context.ChangeQuery(item.Nickname);
						return false;
					}
				});
				//}
			}

			if (link == null && !driverNames.Any(input.StartsWith))
				return results;

			QueryInternal_Directory_Exists(currentPath, input, results);

			return results;

			/*
			// change to search in current directory
			string parentDir = null;
			try {
				parentDir = Path.GetDirectoryName(input);
			}
			catch { }




			return results;
			if (!string.IsNullOrEmpty(parentDir) && results.Count == 0) {
				parentDir = parentDir.TrimEnd('\\', '/');
				//TODO: Why are we doing the following check

				//FUCK THIS CODE o.O!!!!!!!

				if (parentDirectories.ContainsKey(parentDir)) {
					var fuzzy = FuzzyMatcher.Create(Path.GetFileName(currentPath).ToLower());
					foreach (var dir in parentDirectories[parentDir]) {
						if ((dir.Attributes & FileAttributes.Hidden) == FileAttributes.Hidden) continue;

						var matchResult = fuzzy.Evaluate(dir.Name);
						if (!matchResult.Success) continue;

						results.Add(new Result(dir.Name, "Images/folder.png") {
							Score = matchResult.Score,
							Action = (c) => {
								context.ChangeQuery(dir.FullName);
								return false;
							}
						});
					}
				}
			}

			return results;
			*/
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

			if (path != null) {
				var dirs = new DirectoryInfo(path).GetDirectories();

				var parentDirKey = input.TrimEnd('\\', '/');
				if (!parentDirectories.ContainsKey(parentDirKey)) parentDirectories.Add(parentDirKey, dirs);

				var fuzzy = FuzzyMatcher.Create(Path.GetFileName(currentPath).ToLower());
				foreach (var dir in dirs) { //.Where(x => (x.Attributes & FileAttributes.Hidden) != 0)) {
					if ((dir.Attributes & FileAttributes.Hidden) == FileAttributes.Hidden) continue;

					var result = new Result(dir.Name, "Images/folder.png") {
						Action = (c) => {
							context.ChangeQuery(dir.FullName);
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

				//if (results.Count == 0) {
				//	results.Add(new Result("Open this directory", "Images/folder.png", "No files in this directory") {
				//		Action = (c) => {
				//			Process.Start(currentPath);
				//			return true;
				//		}
				//	});
				//}
			}
			else {
				//results.Add(new Result("Open this directory", "Images/folder.png", string.Format("path: {0}", currentPath)) {
				//	Score = 50,
				//	Action = (c) => {
				//		Process.Start(currentPath);
				//		return true;
				//	}
				//});
			}


			/**************************************************************/
			/**************************************************************/

			if (results.Count == 0) {
				results.Add(new Result("Open this directory", "Images/folder.png", "No files in this directory") {
					Action = (c) => {
						Process.Start(currentPath);
						return true;
					}
				});
			}




			/**************************************************************/
			/**************************************************************/

			var Folder = Path.GetDirectoryName(currentPath);
			if (Folder != null) {
				var dirInfo1 = new DirectoryInfo(Folder);
				var fuzzy = FuzzyMatcher.Create(Path.GetFileName(currentPath).ToLower());
				foreach (var dir in dirInfo1.GetFiles()) { //.Where(x => (x.Attributes & FileAttributes.Hidden) != 0)) {
					if ((dir.Attributes & FileAttributes.Hidden) == FileAttributes.Hidden) continue;

					var dirPath = dir.FullName;
					Result result = new Result(System.IO.Path.GetFileNameWithoutExtension(dirPath), dirPath) {
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

					if (Path.GetFileName(currentPath).ToLower() != "") {
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