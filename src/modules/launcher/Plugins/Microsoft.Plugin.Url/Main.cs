using System;
using System.Collections.Generic;
using System.Diagnostics;
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
			}

			return results;
		}

		private string CreateNavigatableUrl(string querySearch)
		{
			throw new NotImplementedException();
		}

		private bool IsUrl(string query)
		{
			return query.Length > 3;
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
				IconPath = "Images/shell.light.png";
			}
			else
			{
				IconPath = "Images/shell.dark.png";
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
