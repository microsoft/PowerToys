using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.RegularExpressions;
using Microsoft.PowerToys.Settings.UI.Lib;
using Wox.Infrastructure.Storage;
using Wox.Plugin;
using Control = System.Windows.Controls.Control;

namespace Microsoft.Plugin.Url
{
	public class Main : IPlugin, ISettingProvider, IPluginI18n, IContextMenu, ISavable
	{
		private PluginJsonStorage<Settings> _storage;
		private Settings _settings;
		private PluginInitContext _context;

		public Main()
		{
			_storage = new PluginJsonStorage<Settings>();
			_settings = _storage.Load();
		}

		public void Save()
		{
			_storage.Save();
		}


		public List<Result> Query(Query query)
		{
			var results = new List<Result>();
			if (IsUrl(query.Search))
			{
				results.Add(new Result()
				{
					Title = query.Search,
					IcoPath = IconPath,
					Action = c =>
					{
						Process.Start(new ProcessStartInfo(CreateNavigatableUrl(query.Search))
						{
							UseShellExecute = true
						});
						return true;
					}
				});
			}

			return results;
		}

		private readonly Regex protocolRegex = new Regex(@"://", RegexOptions.Compiled | RegexOptions.IgnoreCase);

		private string CreateNavigatableUrl(string querySearch)
		{
			return protocolRegex.IsMatch(querySearch)
				? querySearch
				: "https://" + querySearch;
		}

		private readonly Regex ipRegex = new Regex(@"^(?:[0-9]{1,3}\.){3}[0-9]{1,3}$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
		private bool IsUrl(string query)
		{
			return query.Length > 3 &&
				(IsIP(query) || IsHostName(query));
		}


		private bool IsIP(string query)
		{
			return ipRegex.IsMatch(query);
		}

		private readonly Regex hostNameRegex = new Regex(@"[-a-zA-Z0-9@:%._\+~#=]{2,256}\.[a-z]{2,6}\b([-a-zA-Z0-9@:%_\+.~#?&//=]*)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
		private bool IsHostName(string query)
		{
			var match = hostNameRegex.Match(query);

			if (!match.Success)
			{
				return false;
			}
			
			try
			{
				/System.Net.Dns.GetHostEntry(match.Value);
				return true;
			}
			catch
			{
				//Ignore
			}

			return false;
		}

		public void Init(PluginInitContext context)
		{
			// initialize the context of the plugin
			_context = context;
			_storage = new PluginJsonStorage<Settings>();
			_settings = _storage.Load();

			UpdateIconPath(_context.API.GetCurrentTheme());
		}


		// Todo : Update with theme based IconPath
		private void UpdateIconPath(Theme theme)
		{
			if (theme == Theme.Light || theme == Theme.HighContrastWhite)
			{
				IconPath = "Images/url.light.png";
			}
			else
			{
				IconPath = "Images/url.dark.png";
			}
		}

		public string IconPath { get; set; }

		public Control CreateSettingPanel()
		{
			throw new NotImplementedException();
		}

		public void UpdateSettings(PowerLauncherSettings settings)
		{
		}

		public string GetTranslatedPluginTitle()
		{
			return "Url Handler";
		}

		public string GetTranslatedPluginDescription()
		{
			return "Handles urls";
		}

		public List<ContextMenuResult> LoadContextMenus(Result selectedResult)
		{
			return new List<ContextMenuResult>(0);
		}
	}
}
