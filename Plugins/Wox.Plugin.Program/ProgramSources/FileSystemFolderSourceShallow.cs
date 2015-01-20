using System.Collections.Generic;
using System.IO;
using System.Linq;
using Wox.Infrastructure.Storage.UserSettings;

namespace Wox.Plugin.SystemPlugins.Program.ProgramSources {
	//TODO: Consider Removing

	/// <summary>
	/// 
	/// </summary>
	public class FileSystemFolderSourceShallow : FileSystemProgramSource {
		//private static Dictionary<string, DirectoryInfo[]> parentDirectories = new Dictionary<string, DirectoryInfo[]>();


		public FileSystemFolderSourceShallow(string baseDirectory)
			: base(baseDirectory) { }

		public FileSystemFolderSourceShallow(ProgramSource source)
			: base(source) { }

		public override List<Program> LoadPrograms() {
			List<Program> list = new List<Program>();

			foreach (var Folder in Directory.GetDirectories(BaseDirectory)) {
				list.Add(CreateEntry(Folder));
			}


			foreach (string file in Directory.GetFiles(base.BaseDirectory)) {
				if (Suffixes.Any(o => file.EndsWith("." + o))) {
					list.Add(CreateEntry(file));
				}
			}

			return list;
		}


		public override string ToString() {
			return typeof(UserStartMenuProgramSource).Name;
		}


		/*
		public class FolderSource : IProgramSource {
			private PluginInitContext context;
			public string Location { get; set; }
			public int BonusPoints { get; set; }

			public FolderSource(string Location) {
				this.Location = Location;
			}

			public List<Program> LoadPrograms() {
				List<Result> results = new List<Result>();

				if (Directory.Exists(Location)) {
					// show all child directory
					if (Location.EndsWith("\\") || Location.EndsWith("/")) {
						var dirInfo = new DirectoryInfo(Location);
						var dirs = dirInfo.GetDirectories();

						var parentDirKey = Location.TrimEnd('\\', '/');
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
									Process.Start(Location);
									return true;
								}
							};
							results.Add(result);
						}
					}
					else {
						Result result = new Result {
							Title = "Open this directory",
							SubTitle = string.Format("path: {0}", Location),
							Score = 50,
							IcoPath = "Images/folder.png",
							Action = (c) => {
								Process.Start(Location);
								return true;
							}
						};
						results.Add(result);
					}

				}

				// change to search in current directory
				var parentDir = Path.GetDirectoryName(Location);
				if (!string.IsNullOrEmpty(parentDir) && results.Count == 0) {
					parentDir = parentDir.TrimEnd('\\', '/');
					if (parentDirectories.ContainsKey(parentDir)) {

						var dirs = parentDirectories[parentDir];
						var queryFileName = Path.GetFileName(Location).ToLower();
						var fuzzy = FuzzyMatcher.Create(queryFileName);
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


				throw new Exception("Debug this!");
			}

		}
		*/
	}
}
