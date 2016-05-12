using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Wox.Core.UserSettings;
using Wox.Infrastructure.Exception;
using Wox.Infrastructure.Logger;
using Wox.Plugin;

namespace Wox.Core.Plugin
{
    public static class PluginsLoader
    {
        public const string PATH = "PATH";
        public const string Python = "python";
        public const string PythonExecutable = "pythonw.exe";

        public static List<PluginPair> Plugins(List<PluginMetadata> metadatas, PluginsSettings settings)
        {
            var csharpPlugins = CSharpPlugins(metadatas).ToList();
            var pythonPlugins = PythonPlugins(metadatas, settings.PythonDirectory);
            var executablePlugins = ExecutablePlugins(metadatas);
            var plugins = csharpPlugins.Concat(pythonPlugins).Concat(executablePlugins).ToList();
            return plugins;
        }

        public static IEnumerable<PluginPair> CSharpPlugins(List<PluginMetadata> source)
        {
            var plugins = new List<PluginPair>();
            var metadatas = source.Where(o => o.Language.ToUpper() == AllowedLanguage.CSharp);

            foreach (var metadata in metadatas)
            {
                Assembly assembly;
                try
                {
                    assembly = Assembly.Load(AssemblyName.GetAssemblyName(metadata.ExecuteFilePath));
                }
                catch (Exception e)
                {
                    Log.Error(new WoxPluginException(metadata.Name, "Couldn't load assembly", e));
                    continue;
                }
                var types = assembly.GetTypes();
                Type type;
                try
                {
                    type = types.First(o => o.IsClass && !o.IsAbstract && o.GetInterfaces().Contains(typeof(IPlugin)));
                }
                catch (InvalidOperationException e)
                {
                    Log.Error(new WoxPluginException(metadata.Name, "Can't find class implement IPlugin", e));
                    continue;
                }
                IPlugin plugin;
                try
                {
                    plugin = (IPlugin)Activator.CreateInstance(type);
                }
                catch (Exception e)
                {
                    Log.Error(new WoxPluginException(metadata.Name, "Can't create instance", e));
                    continue;
                }
                PluginPair pair = new PluginPair
                {
                    Plugin = plugin,
                    Metadata = metadata
                };
                plugins.Add(pair);
            }
            return plugins;
        }

        public static IEnumerable<PluginPair> PythonPlugins(List<PluginMetadata> source, string pythonDirecotry)
        {
            var metadatas = source.Where(o => o.Language.ToUpper() == AllowedLanguage.Python);
            string filename;

            if (string.IsNullOrEmpty(pythonDirecotry))
            {
                var paths = Environment.GetEnvironmentVariable(PATH);
                if (paths != null)
                {
                    var pythonPaths = paths.Split(';').Where(p => p.ToLower().Contains(Python));
                    if (pythonPaths.Any())
                    {
                        filename = PythonExecutable;
                    }
                    else
                    {
                        Log.Error(new WoxException("Python can't be found in PATH."));
                        return new List<PluginPair>();
                    }
                }
                else
                {
                    Log.Error(new WoxException("Path variable is not set."));
                    return new List<PluginPair>();
                }
            }
            else
            {
                var path = Path.Combine(pythonDirecotry, PythonExecutable);
                if (File.Exists(path))
                {
                    filename = path;
                }
                else
                {
                    Log.Error(new WoxException("Can't find python executable in python directory"));
                    return new List<PluginPair>();
                }
            }
            var plugins = metadatas.Select(metadata => new PluginPair
            {
                Plugin = new PythonPlugin(filename),
                Metadata = metadata
            });
            return plugins;
        }

        public static IEnumerable<PluginPair> ExecutablePlugins(IEnumerable<PluginMetadata> source)
        {
            var metadatas = source.Where(o => o.Language.ToUpper() == AllowedLanguage.Executable);

            var plugins = metadatas.Select(metadata => new PluginPair
            {
                Plugin = new ExecutablePlugin(metadata.ExecuteFilePath),
                Metadata = metadata
            });
            return plugins;
        }

    }
}