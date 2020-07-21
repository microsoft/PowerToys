using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Plugin.Uri.UriHelper;
using Microsoft.PowerToys.Settings.UI.Lib;
using Wox.Infrastructure.Storage;
using Wox.Plugin;
using Control = System.Windows.Controls.Control;

namespace Microsoft.Plugin.Uri
{
	public class Main : IPlugin, ISettingProvider, IPluginI18n, IContextMenu, ISavable
	{
		private PluginJsonStorage<Settings> _storage;
		private Settings _settings;
		private PluginInitContext _context;
		private ExtendedUriParser _uriParser;
		private UriResolver _uriResolver;

		public Main()
		{
			_storage = new PluginJsonStorage<Settings>();
			_settings = _storage.Load();
			_uriParser = new ExtendedUriParser();
			_uriResolver = new UriResolver();
		}

		public void Save()
		{
			_storage.Save();
		}

		public List<Result> Query(Query query)
		{
			var results = new List<Result>();

			if (_uriParser.TryParse(query.Search, out var uriResult)
				&& _uriResolver.IsValidHostAsync(uriResult).GetAwaiter().GetResult())
			{
				var uriResultString = uriResult.ToString();

				results.Add(new Result()
				{
					Title = uriResultString,
					IcoPath = IconPath,
					Action = action =>
					{
						Process.Start(new ProcessStartInfo(uriResultString)
						{
							UseShellExecute = true
						});
						return true;
					}
				});
			}

			return results;
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
				IconPath = "Images/uri.light.png";
			}
			else
			{
				IconPath = "Images/uri.dark.png";
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
