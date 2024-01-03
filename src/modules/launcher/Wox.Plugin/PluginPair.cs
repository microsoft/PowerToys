// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Windows;
using Microsoft.PowerToys.Settings.UI.Library;
using Wox.Plugin.Logger;
using Wox.Plugin.Properties;

namespace Wox.Plugin
{
    public class PluginPair
    {
        public IPlugin Plugin { get; internal set; }

        public PluginMetadata Metadata { get; internal set; }

        public PluginPair(PluginMetadata metadata)
        {
            this.Metadata = metadata;
            LoadPlugin();
        }

        public bool IsPluginInitialized { get; set; }

        public void InitializePlugin(IPublicAPI api)
        {
            if (Metadata.Disabled)
            {
                Log.Info($"Do not initialize {Metadata.Name} as it is disabled.", GetType());
                return;
            }

            if (IsPluginInitialized)
            {
                Log.Info($"{Metadata.Name} plugin is already initialized", GetType());
                return;
            }

            var stopWatch = new Stopwatch();
            stopWatch.Start();
            if (!InitPlugin(api))
            {
                return;
            }

            stopWatch.Stop();
            IsPluginInitialized = true;
            Metadata.InitTime += stopWatch.ElapsedMilliseconds;
            Log.Info($"Total initialize cost for <{Metadata.Name}> is <{Metadata.InitTime}ms>", GetType());
            return;
        }

        public void Update(PowerLauncherPluginSettings setting, IPublicAPI api, Action refreshPluginsOverviewCallback)
        {
            if (setting == null || api == null)
            {
                return;
            }

            bool refreshOverview = false;
            if (Metadata.Disabled != setting.Disabled
                || Metadata.ActionKeyword != setting.ActionKeyword)
            {
                refreshOverview = true;
            }

            // If the enabled state is policy managed then we skip the update of the disabled state as it must be a manual settings.json manipulation.
            if (!Metadata.IsEnabledPolicyConfigured)
            {
                if (Metadata.Disabled && !setting.Disabled)
                {
                    Metadata.Disabled = false;
                    InitializePlugin(api);

                    if (!IsPluginInitialized)
                    {
                        string description = $"{Resources.FailedToLoadPluginDescription} {Metadata.Name}\n\n{Resources.FailedToLoadPluginDescriptionPartTwo}";
                        Application.Current.Dispatcher.InvokeAsync(() => api.ShowMsg(Resources.FailedToLoadPluginTitle, description, string.Empty, false));
                    }
                }
                else
                {
                    Metadata.Disabled = setting.Disabled;
                }
            }

            Metadata.ActionKeyword = setting.ActionKeyword;
            Metadata.WeightBoost = setting.WeightBoost;

            Metadata.IsGlobal = setting.IsGlobal;

            (Plugin as ISettingProvider)?.UpdateSettings(setting);

            if (IsPluginInitialized && !Metadata.Disabled)
            {
                (Plugin as IReloadable)?.ReloadData();
            }

            if (refreshOverview)
            {
                refreshPluginsOverviewCallback?.Invoke();
            }
        }

        public override string ToString()
        {
            return Metadata.Name;
        }

        public override bool Equals(object obj)
        {
            if (obj is PluginPair r)
            {
                // Using Ordinal since this is used internally
                return string.Equals(r.Metadata.ID, Metadata.ID, StringComparison.Ordinal);
            }
            else
            {
                return false;
            }
        }

        public override int GetHashCode()
        {
            // Using Ordinal since this is used internally
            var hashcode = Metadata.ID?.GetHashCode(StringComparison.Ordinal) ?? 0;
            return hashcode;
        }

        private void LoadPlugin()
        {
            var stopWatch = new Stopwatch();
            stopWatch.Start();
            CreatePluginInstance();
            stopWatch.Stop();
            Metadata.InitTime += stopWatch.ElapsedMilliseconds;
            Log.Info($"Load cost for <{Metadata.Name}> is <{Metadata.InitTime}ms>", GetType());
        }

        private bool CreatePluginInstance()
        {
            if (Plugin != null)
            {
                Log.Warn($"{Metadata.Name} plugin was already loaded", GetType());
                return true;
            }

            try
            {
                if (Metadata.DynamicLoading)
                {
                    var loadContext = new PluginLoadContext(Metadata.ExecuteFilePath);
                    _assembly = loadContext.LoadFromAssemblyName(new AssemblyName(Path.GetFileNameWithoutExtension(Metadata.ExecuteFilePath)));
                }
                else
                {
                    _assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(Metadata.ExecuteFilePath);
                }
            }
            catch (Exception e)
            {
                Log.Exception($"Couldn't load assembly for {Metadata.Name} in {Metadata.ExecuteFilePath}", e, MethodBase.GetCurrentMethod().DeclaringType);
                return false;
            }

            Type[] types;
            try
            {
                types = _assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException e)
            {
                Log.Exception($"Couldn't get assembly types for {Metadata.Name} in {Metadata.ExecuteFilePath}. The plugin might be corrupted. Uninstall PowerToys, manually delete the install folder and reinstall.", e, MethodBase.GetCurrentMethod().DeclaringType);
                return false;
            }

            Type type;
            try
            {
                type = types.First(o => o.IsClass && !o.IsAbstract && o.GetInterfaces().Contains(typeof(IPlugin)));
            }
            catch (InvalidOperationException e)
            {
                Log.Exception($"Can't find class implement IPlugin for <{Metadata.Name}> in {Metadata.ExecuteFilePath}", e, MethodBase.GetCurrentMethod().DeclaringType);
                return false;
            }

            // Validate plugin ID to prevent bypassing the GPO by changing the ID in the plugin.json file.
            string pluginID = (string)type.GetProperty("PluginID", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
            if (pluginID == null)
            {
                Log.Error($"Can't validate plugin ID of plugin <{Metadata.Name}> in {Metadata.ExecuteFilePath}: The static property <Main.PluginID> was not found.", MethodBase.GetCurrentMethod().DeclaringType);
                return false;
            }
            else if (pluginID != Metadata.ID)
            {
                Log.Error($"Wrong plugin ID found in plugin.json of plugin <{Metadata.Name}>. ('{Metadata.ID}' != '{pluginID}')", MethodBase.GetCurrentMethod().DeclaringType);
                return false;
            }

            try
            {
                Plugin = (IPlugin)Activator.CreateInstance(type);
            }
            catch (Exception e)
            {
                Log.Exception($"Can't create instance for <{Metadata.Name}> in {Metadata.ExecuteFilePath}", e, MethodBase.GetCurrentMethod().DeclaringType);
                return false;
            }

            return true;
        }

        private bool InitPlugin(IPublicAPI api)
        {
            if (Plugin == null)
            {
                Log.Warn($"Can not initialize {Metadata.Name} plugin as it was not loaded", GetType());
                return false;
            }

            try
            {
                Plugin.Init(new PluginInitContext
                {
                    CurrentPluginMetadata = Metadata,
                    API = api,
                });
            }
            catch (Exception e)
            {
                Log.Exception($"Fail to Init plugin: {Metadata.Name}", e, GetType());
                return false;
            }

            return true;
        }

        private Assembly _assembly;
    }
}
