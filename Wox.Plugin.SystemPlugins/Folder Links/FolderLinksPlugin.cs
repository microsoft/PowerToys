using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Wox.Infrastructure.Storage.UserSettings;

namespace Wox.Plugin.SystemPlugins.Folder_Links {
	public class FolderLinksPlugin : BaseSystemPlugin, ISettingProvider {
		private PluginInitContext context;

		//private static List<string> driverNames = null;
		private static Dictionary<string, DirectoryInfo[]> parentDirectories = new Dictionary<string, DirectoryInfo[]>();

		public override string Name { get { return "Folder Links"; } }
		public override string Description { get { return "Adds additional folders you can search"; } }
		public override string IcoPath { get { return @"Images\folder.png"; } }

		protected override void InitInternal(PluginInitContext context) {
			this.context = context;
		}

		public System.Windows.Controls.Control CreateSettingPanel() {
			//return new WebSearchesSetting();			
			return new FolderLinksSettings();
		}

		protected override List<Result> QueryInternal(Query query) {
			var Saved_Folders = UserSettingStorage.Instance.FolderLinks.Select(x => x.Path).ToList();

			//TODO: Consider always clearing the cache
			List<Result> results = new List<Result>();
			if (string.IsNullOrEmpty(query.RawQuery)) {
				// clear the cache
				if (parentDirectories.Count > 0)
					parentDirectories.Clear();

				return results;
			}

			var input = query.RawQuery.ToLower();
			var Input_Name = input.Split(new string[] { @"\" }, StringSplitOptions.None).First().ToLower();
			var Current_Path = Saved_Folders.FirstOrDefault(x =>
				x.Split(new string[] { @"\" }, StringSplitOptions.None).Last().ToLower() == Input_Name);
			if (Current_Path == null) return results;

			Current_Path += query.RawQuery.ToLower().Remove(0, Input_Name.Length);

			if (Directory.Exists(Current_Path)) {
				// show all child directory
				if (input.EndsWith("\\") || input.EndsWith("/")) {
					var dirInfo = new DirectoryInfo(Current_Path);
					var dirs = dirInfo.GetDirectories();

					var parentDirKey = input.TrimEnd('\\', '/');
					if (!parentDirectories.ContainsKey(parentDirKey))
						parentDirectories.Add(parentDirKey, dirs);

					foreach (var dir in dirs) {
						if ((dir.Attributes & FileAttributes.Hidden) == FileAttributes.Hidden)
							continue;

						var dirPath = dir.FullName;
						Result result = new Result {
							Title = dir.Name,
							IcoPath = "Images/folder.png",
							Action = (c) => {
								context.ChangeQuery(dirPath);
								return false;
							}
						};
						results.Add(result);
					}

					if (results.Count == 0) {
						Result result = new Result {
							Title = "Open this directory",
							SubTitle = "No files in this directory",
							IcoPath = "Images/folder.png",
							Action = (c) => {
								Process.Start(Current_Path);
								return true;
							}
						};
						results.Add(result);
					}
				}
				else {
					Result result = new Result {
						Title = "Open this directory",
						SubTitle = string.Format("path: {0}", Current_Path),
						Score = 50,
						IcoPath = "Images/folder.png",
						Action = (c) => {
							Process.Start(Current_Path);
							return true;
						}
					};
					results.Add(result);
				}

			}

			// change to search in current directory
			var parentDir = Path.GetDirectoryName(input);
			if (!string.IsNullOrEmpty(parentDir) && results.Count == 0) {
				parentDir = parentDir.TrimEnd('\\', '/');
				if (parentDirectories.ContainsKey(parentDir)) {

					var dirs = parentDirectories[parentDir];
					var queryFileName = Path.GetFileName(Current_Path).ToLower();
					var fuzzy = Wox.Infrastructure.FuzzyMatcher.Create(queryFileName);
					foreach (var dir in dirs) {
						if ((dir.Attributes & FileAttributes.Hidden) == FileAttributes.Hidden)
							continue;

						var matchResult = fuzzy.Evaluate(dir.Name);
						if (!matchResult.Success)
							continue;

						var dirPath = dir.FullName;
						Result result = new Result {
							Title = dir.Name,
							IcoPath = "Images/folder.png",
							Score = matchResult.Score,
							Action = (c) => {
								context.ChangeQuery(dirPath);
								return false;
							}
						};
						results.Add(result);
					}
				}
			}


			return results;
		}


	}
}
