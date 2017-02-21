using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using JetBrains.Annotations;
using Wox.Core.Plugin;
using Wox.Infrastructure;
using Wox.Infrastructure.Exception;
using Wox.Infrastructure.Logger;
using Wox.Infrastructure.UserSettings;
using Wox.Plugin;

namespace Wox.Core.Resource
{
    public class Internationalization
    {
        public Settings Settings { get; set; }
        private const string DirectoryName = "Languages";
        private readonly List<string> _languageDirectories = new List<string>();
        private readonly List<ResourceDictionary> _oldResources = new List<ResourceDictionary>();

        public Internationalization()
        {
            var woxThemeDirectory = Path.Combine(Constant.ProgramDirectory, DirectoryName);
            _languageDirectories.Add(woxThemeDirectory);

            foreach (var plugin in PluginManager.GetPluginsForInterface<IPluginI18n>())
            {
                var location = Assembly.GetAssembly(plugin.Plugin.GetType()).Location;
                var dir = Path.GetDirectoryName(location);
                if (dir != null)
                {
                    var pluginThemeDirectory = Path.Combine(dir, DirectoryName);
                    _languageDirectories.Add(pluginThemeDirectory);
                }
                else
                {
                    Log.Error($"|ResourceMerger.UpdatePluginLanguages|Can't find plugin path <{location}> for <{plugin.Metadata.Name}>");
                }
            }
        }

        public void ChangeLanguage(string languageCode)
        {
            languageCode = languageCode.NonNull();
            Language language = GetLanguageByLanguageCode(languageCode);
            ChangeLanguage(language);
        }

        private Language GetLanguageByLanguageCode(string languageCode)
        {
            var lowercase = languageCode.ToLower();
            var language = AvailableLanguages.GetAvailableLanguages().FirstOrDefault(o => o.LanguageCode.ToLower() == lowercase);
            if (language == null)
            {
                Log.Error($"|Internationalization.GetLanguageByLanguageCode|Language code can't be found <{languageCode}>");
                return AvailableLanguages.English;
            }
            else
            {
                return language;
            }
        }

        public void ChangeLanguage(Language language)
        {
            language = language.NonNull();

            var files = _languageDirectories.Select(LanguageFile).Where(f => !string.IsNullOrEmpty(f)).ToArray();

            if (files.Length > 0)
            {
                Settings.Language = language.LanguageCode;

                var dicts = Application.Current.Resources.MergedDictionaries;
                foreach (var r in _oldResources)
                {
                    dicts.Remove(r);
                }
                foreach (var f in files)
                {
                    var r = new ResourceDictionary
                    {
                        Source = new Uri(f, UriKind.Absolute)
                    };
                    dicts.Add(r);
                    _oldResources.Add(r);
                }
            }

            foreach (var plugin in PluginManager.GetPluginsForInterface<IPluginI18n>())
            {
                UpdatePluginMetadataTranslations(plugin);
            }
        }


        public List<Language> LoadAvailableLanguages()
        {
            return AvailableLanguages.GetAvailableLanguages();
        }

        public string GetTranslation(string key)
        {
            var translation = Application.Current.TryFindResource(key);
            if (translation is string)
            {
                return translation.ToString();
            }
            else
            {
                return "NoTranslation";
            }
        }

        private void UpdatePluginMetadataTranslations(PluginPair pluginPair)
        {
            var pluginI18n = pluginPair.Plugin as IPluginI18n;
            if (pluginI18n == null) return;
            try
            {
                pluginPair.Metadata.Name = pluginI18n.GetTranslatedPluginTitle();
                pluginPair.Metadata.Description = pluginI18n.GetTranslatedPluginDescription();
            }
            catch (Exception e)
            {
                Log.Exception($"|Internationalization.UpdatePluginMetadataTranslations|Update Plugin metadata translation failed for <{pluginPair.Metadata.Name}>", e);
            }
        }

        public string LanguageFile(string folder)
        {
            if (Directory.Exists(folder))
            {
                string path = Path.Combine(folder, Settings.Language + ".xaml");
                if (File.Exists(path))
                {
                    return path;
                }
                else
                {
                    Log.Error($"|Internationalization.LanguageFile|Language path can't be found <{path}>");
                    string english = Path.Combine(folder, "en.xaml");
                    if (File.Exists(english))
                    {
                        return english;
                    }
                    else
                    {
                        Log.Error($"|Internationalization.LanguageFile|Default English Language path can't be found <{path}>");
                        return string.Empty;
                    }
                }
            }
            else
            {
                return string.Empty;
            }
        }
    }

}
