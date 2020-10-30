// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using Wox.Infrastructure;
using Wox.Infrastructure.UserSettings;
using Wox.Plugin;
using Wox.Plugin.Logger;

namespace Wox.Core.Plugin
{
    public static class PluginsLoader
    {
        public const string PATH = "PATH";

        public static List<PluginPair> Plugins(List<PluginMetadata> metadatas)
        {
            var csharpPlugins = CSharpPlugins(metadatas).ToList();
            return csharpPlugins;
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Suppressing this to enable FxCop. We are logging the exception, and going forward general exceptions should not be caught")]
        public static IEnumerable<PluginPair> CSharpPlugins(List<PluginMetadata> source)
        {
            var plugins = new List<PluginPair>();
            var metadatas = source.Where(o => o.Language.ToUpperInvariant() == AllowedLanguage.CSharp);

            foreach (var metadata in metadatas)
            {
                var milliseconds = Stopwatch.Debug($"|PluginsLoader.CSharpPlugins|Constructor init cost for {metadata.Name}", () =>
                {
#if DEBUG
                    var assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(metadata.ExecuteFilePath);
                    var types = assembly.GetTypes();
                    var type = types.First(o => o.IsClass && !o.IsAbstract && o.GetInterfaces().Contains(typeof(IPlugin)));
                    var plugin = (IPlugin)Activator.CreateInstance(type);
#else
                    Assembly assembly;
                    try
                    {
                        assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(metadata.ExecuteFilePath);
                    }
                    catch (Exception e)
                    {
                        Log.Exception($"Couldn't load assembly for {metadata.Name}", e, MethodBase.GetCurrentMethod().DeclaringType);
                        return;
                    }

                    var types = assembly.GetTypes();
                    Type type;
                    try
                    {
                        type = types.First(o => o.IsClass && !o.IsAbstract && o.GetInterfaces().Contains(typeof(IPlugin)));
                    }
                    catch (InvalidOperationException e)
                    {
                        Log.Exception($"Can't find class implement IPlugin for <{metadata.Name}>", e, MethodBase.GetCurrentMethod().DeclaringType);
                        return;
                    }

                    IPlugin plugin;
                    try
                    {
                        plugin = (IPlugin)Activator.CreateInstance(type);
                    }
                    catch (Exception e)
                    {
                        Log.Exception($"Can't create instance for <{metadata.Name}>", e, MethodBase.GetCurrentMethod().DeclaringType);
                        return;
                    }
#endif
                    PluginPair pair = new PluginPair
                    {
                        Plugin = plugin,
                        Metadata = metadata,
                    };
                    plugins.Add(pair);
                });
                metadata.InitTime += milliseconds;
            }

            return plugins;
        }
    }
}
