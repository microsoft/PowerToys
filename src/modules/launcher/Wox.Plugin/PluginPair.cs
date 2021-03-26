// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
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
        }

        public bool IsPluginLoaded { get; internal set; }

        public void LoadPlugin(IPublicAPI api)
        {
            if (Metadata.Disabled)
            {
                Log.Info($"Do not load {Metadata.Name} as it is disabled.", GetType());
                return;
            }

            if (IsPluginLoaded)
            {
                Log.Info($"Plugin {Metadata.Name} is already loaded", GetType());
                return;
            }

            var stopWatch = new Stopwatch();
            stopWatch.Start();
            if (!CreatePluginInstance())
            {
                return;
            }

            if (!InitPlugin(api))
            {
                return;
            }

            stopWatch.Stop();
            IsPluginLoaded = true;
            Metadata.InitTime += stopWatch.ElapsedMilliseconds;
            Log.Info($"Total load cost for <{Metadata.Name}> is <{Metadata.InitTime}ms>", GetType());
            return;
        }

        public void Update(PowerLauncherPluginSettings setting, IPublicAPI api)
        {
            if (setting == null || api == null)
            {
                return;
            }

            if (Metadata.Disabled && !setting.Disabled)
            {
                Metadata.Disabled = false;
                LoadPlugin(api);
                if (!IsPluginLoaded)
                {
                    var title = string.Format(CultureInfo.CurrentCulture, Resources.FailedToLoadPluginTitle, Metadata.Name);
                    api.ShowMsg(title, Resources.FailedToLoadPluginDescription, string.Empty, false);
                }
            }
            else
            {
                Metadata.Disabled = setting.Disabled;
            }

            Metadata.ActionKeyword = setting.ActionKeyword;
            Metadata.IsGlobal = setting.IsGlobal;
            if (Plugin is ISettingProvider)
            {
                (Plugin as ISettingProvider).UpdateSettings(setting);
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

        private bool CreatePluginInstance()
        {
            try
            {
                _assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(Metadata.ExecuteFilePath);
            }
#pragma warning disable CA1031 // Do not catch general exception types
            catch (Exception e)
#pragma warning restore CA1031 // Do not catch general exception types
            {
                Log.Exception($"Couldn't load assembly for {Metadata.Name}", e, MethodBase.GetCurrentMethod().DeclaringType);
                return false;
            }

            var types = _assembly.GetTypes();
            Type type;
            try
            {
                type = types.First(o => o.IsClass && !o.IsAbstract && o.GetInterfaces().Contains(typeof(IPlugin)));
            }
            catch (InvalidOperationException e)
            {
                Log.Exception($"Can't find class implement IPlugin for <{Metadata.Name}>", e, MethodBase.GetCurrentMethod().DeclaringType);
                return false;
            }

            try
            {
                Plugin = (IPlugin)Activator.CreateInstance(type);
            }
#pragma warning disable CA1031 // Do not catch general exception types
            catch (Exception e)
#pragma warning restore CA1031 // Do not catch general exception types
            {
                Log.Exception($"Can't create instance for <{Metadata.Name}>", e, MethodBase.GetCurrentMethod().DeclaringType);
                return false;
            }

            return true;
        }

        private bool InitPlugin(IPublicAPI api)
        {
            try
            {
                Plugin.Init(new PluginInitContext
                {
                    CurrentPluginMetadata = Metadata,
                    API = api,
                });
            }
#pragma warning disable CA1031 // Do not catch general exception types
            catch (Exception e)
#pragma warning restore CA1031 // Do not catch general exception types
            {
                Log.Exception($"Fail to Init plugin: {Metadata.Name}", e, GetType());
                return false;
            }

            return true;
        }

        private Assembly _assembly;
    }
}
