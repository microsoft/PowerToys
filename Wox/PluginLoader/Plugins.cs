using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.CSharp;
using Wox.Helper;
using Wox.Infrastructure;
using Wox.Infrastructure.Storage;
using Wox.Infrastructure.Storage.UserSettings;
using Wox.Plugin;

namespace Wox.PluginLoader {
	public static class Plugins {
		//private static string debuggerMode = null;
		public static String DebuggerMode { get; private set; }

		private static List<PluginPair> plugins = new List<PluginPair>();
		private static ManualResetEvent initializing = null;

		public static void Init() {
			if (initializing != null) return;

			initializing = new ManualResetEvent(false);
			plugins.Clear();
			BasePluginLoader.ParsePluginsConfig();
		
			if (UserSettingStorage.Instance.EnablePythonPlugins) {
				plugins.AddRange(new PythonPluginLoader().LoadPlugin());
			}

			plugins.AddRange(new CSharpPluginLoader().LoadPlugin());
			Forker forker = new Forker();
			foreach (IPlugin plugin in plugins.Select(pluginPair => pluginPair.Plugin)) {
				IPlugin plugin1 = plugin;
				PluginPair pluginPair = plugins.FirstOrDefault(o => o.Plugin == plugin1);
				if (pluginPair != null) {
					PluginMetadata metadata = pluginPair.Metadata;
					pluginPair.InitContext = new PluginInitContext() {
						Plugins = plugins,
						CurrentPluginMetadata = metadata,
						ChangeQuery = s => App.Window.Dispatcher.Invoke(new Action(() => App.Window.ChangeQuery(s))),
						CloseApp = () => App.Window.Dispatcher.Invoke(new Action(() => App.Window.CloseApp())),
						HideApp = () => App.Window.Dispatcher.Invoke(new Action(() => App.Window.HideApp())),
						ShowApp = () => App.Window.Dispatcher.Invoke(new Action(() => App.Window.ShowApp())),
						ShowMsg = (title, subTitle, iconPath) => App.Window.Dispatcher.Invoke(new Action(() =>
							App.Window.ShowMsg(title, subTitle, iconPath))),
						OpenSettingDialog = () => App.Window.Dispatcher.Invoke(new Action(() => App.Window.OpenSettingDialog())),
						ShowCurrentResultItemTooltip = (msg) => App.Window.Dispatcher.Invoke(new Action(() => App.Window.ShowCurrentResultItemTooltip(msg))),
						ReloadPlugins = () => App.Window.Dispatcher.Invoke(new Action(() => Init())),
						InstallPlugin = (filePath) => App.Window.Dispatcher.Invoke(new Action(() => {
							PluginInstaller.Install(filePath);
						})),
						StartLoadingBar = () => App.Window.Dispatcher.Invoke(new Action(() => App.Window.StartLoadingBar())),
						StopLoadingBar = () => App.Window.Dispatcher.Invoke(new Action(() => App.Window.StopLoadingBar())),
						//ShellRun = (cmd) => (bool)App.Window.Dispatcher.Invoke(new Func<bool>(() => App.Window.ShellRun(cmd)))
					};

					pluginPair.InitContext.ShellRun = (cmd) => {
						try {
							return (bool)App.Window.Dispatcher.Invoke(new Func<bool>(() => App.Window.ShellRun(cmd)));
						}
						catch (Exception) {
							return false;
						}
					};

					forker.Fork(() => plugin1.Init(pluginPair.InitContext));
				}
			}

			ThreadPool.QueueUserWorkItem(o => {
				forker.Join();
				initializing.Set();
				initializing = null;
			});
		}

		public static List<PluginPair> AllPlugins {
			get {
				var init = initializing;
				if (init != null) {
					init.WaitOne();
				}
				return plugins;
			}
		}

		public static bool HitThirdpartyKeyword(Query query) {
			if (string.IsNullOrEmpty(query.ActionName)) return false;

			return plugins.Any(o => o.Metadata.PluginType == PluginType.ThirdParty && o.Metadata.ActionKeyword == query.ActionName);
		}

		public static void ActivatePluginDebugger(string path) {
			DebuggerMode = path;
		}
	}
}
