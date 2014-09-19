using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using Newtonsoft.Json;
using Wox.Helper;
using Wox.Infrastructure.Logger;
using Wox.Infrastructure.Storage.UserSettings;
using Wox.Plugin;
using Wox.Plugin.SystemPlugins;

namespace Wox.PluginLoader {
	public abstract class PluginConfigLoader {
		private static string PluginPath = Path.Combine(Path.GetDirectoryName(System.Windows.Forms.Application.ExecutablePath), "Plugins");
		private static string PluginConfigName = "plugin.json";
		private static List<PluginMetadata> pluginMetadatas = new List<PluginMetadata>();

        public static List<PluginMetadata> ParsePluginsConfig()
        {
			pluginMetadatas.Clear();
			ParseSystemPlugins();
			ParseThirdPartyPlugins();

			if (Plugins.DebuggerMode != null) {
				PluginMetadata metadata = GetMetadataFromJson(Plugins.DebuggerMode);
				if (metadata != null) pluginMetadatas.Add(metadata);
			}
		    return pluginMetadatas;
		}

		private static void ParseSystemPlugins() {
			pluginMetadatas.Add(new PluginMetadata() {
				Name = "System Plugins",
				Author = "System",
				Description = "system plugins collection",
                Website = "http://www.getwox.com",
				Language = AllowedLanguage.CSharp,
				Version = "1.0",
				PluginType = PluginType.System,
				ActionKeyword = "*",
				ExecuteFileName = "Wox.Plugin.SystemPlugins.dll",
				PluginDirectory = Path.GetDirectoryName(System.Windows.Forms.Application.ExecutablePath)
			});
		}

		private static void ParseThirdPartyPlugins() {
			if (!Directory.Exists(PluginPath))
				Directory.CreateDirectory(PluginPath);

			string[] directories = Directory.GetDirectories(PluginPath);
			foreach (string directory in directories) {
				if (File.Exists((Path.Combine(directory, "NeedDelete.txt")))) {
					Directory.Delete(directory, true);
					continue;
				}
				PluginMetadata metadata = GetMetadataFromJson(directory);
				if (metadata != null) pluginMetadatas.Add(metadata);
			}
		}

		private static PluginMetadata GetMetadataFromJson(string pluginDirectory) {
			string configPath = Path.Combine(pluginDirectory, PluginConfigName);
			PluginMetadata metadata;

			if (!File.Exists(configPath)) {
				Log.Warn(string.Format("parse plugin {0} failed: didn't find config file.", configPath));
				return null;
			}

			try {
				metadata = JsonConvert.DeserializeObject<PluginMetadata>(File.ReadAllText(configPath));
				metadata.PluginType = PluginType.ThirdParty;
				metadata.PluginDirectory = pluginDirectory;
			}
			catch (Exception) {
				string error = string.Format("Parse plugin config {0} failed: json format is not valid", configPath);
				Log.Warn(error);
#if (DEBUG)
				{
					throw new WoxException(error);
				}
#endif
				return null;
			}


			if (!AllowedLanguage.IsAllowed(metadata.Language)) {
				string error = string.Format("Parse plugin config {0} failed: invalid language {1}", configPath, metadata.Language);
				Log.Warn(error);
#if (DEBUG)
				{
					throw new WoxException(error);
				}
#endif
				return null;
			}
			if (!File.Exists(metadata.ExecuteFilePath)) {
				string error = string.Format("Parse plugin config {0} failed: ExecuteFile {1} didn't exist", configPath, metadata.ExecuteFilePath);
				Log.Warn(error);
#if (DEBUG)
				{
					throw new WoxException(error);
				}
#endif
				return null;
			}

		    var customizedPluginConfig =
		        UserSettingStorage.Instance.CustomizedPluginConfigs.FirstOrDefault(o => o.ID == metadata.ID);
		    if (customizedPluginConfig != null && !string.IsNullOrEmpty(customizedPluginConfig.Actionword))
		    {
		        metadata.ActionKeyword = customizedPluginConfig.Actionword;
		    }

			return metadata;
		}
	}
}
