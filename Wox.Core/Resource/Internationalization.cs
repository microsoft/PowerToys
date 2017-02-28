using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using Wox.Core.Plugin;
using Wox.Infrastructure;
using Wox.Infrastructure.Logger;
using Wox.Infrastructure.UserSettings;
using Wox.Plugin;

namespace Wox.Core.Resource
{
    public class Internationalization
    {
        public Settings Settings { get; set; }
        private const string Folder = "Languages";
        private const string DefaultFile = "en.xaml";
        private const string Extension = ".xaml";
        private readonly List<string> _languageDirectories = new List<string>();
        private readonly List<ResourceDictionary> _oldResources = new List<ResourceDictionary>();

        public Internationalization()
        {
            AddPluginLanguageDirectories();
            LoadDefaultLanguage();
            // we don't want to load /Languages/en.xaml twice
            // so add wox language directory after load plugin language files
            AddWoxLanguageDirectory();
        }


        private void AddWoxLanguageDirectory()
        {
            var directory = Path.Combine(Constant.ProgramDirectory, Folder);
            _languageDirectories.Add(directory);
        }


        private void AddPluginLanguageDirectories()
        {
            foreach (var plugin in PluginManager.GetPluginsForInterface<IPluginI18n>())
            {
                var location = Assembly.GetAssembly(plugin.Plugin.GetType()).Location;
                var dir = Path.GetDirectoryName(location);
                if (dir != null)
                {
                    var pluginThemeDirectory = Path.Combine(dir, Folder);
                    _languageDirectories.Add(pluginThemeDirectory);
                }
                else
                {
                    Log.Error($"|Internationalization.AddPluginLanguageDirectories|Can't find plugin path <{location}> for <{plugin.Metadata.Name}>");
                }
            }
        }

        private void LoadDefaultLanguage()
        {
            LoadLanguage(AvailableLanguages.English);
            _oldResources.Clear();
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

            Settings.Language = language.LanguageCode;

            RemoveOldLanguageFiles();
            if (language != AvailableLanguages.English)
            {
                LoadLanguage(language);
            }
            UpdatePluginMetadataTranslations();

        }

        private void RemoveOldLanguageFiles()
        {
            var dicts = Application.Current.Resources.MergedDictionaries;
            foreach (var r in _oldResources)
            {
                dicts.Remove(r);
            }
        }

        private void LoadLanguage(Language language)
        {
            var dicts = Application.Current.Resources.MergedDictionaries;
            var filename = $"{language.LanguageCode}{Extension}";
            var files = _languageDirectories
                .Select(d => LanguageFile(d, filename))
                .Where(f => !string.IsNullOrEmpty(f))
                .ToArray();

            if (files.Length > 0)
            {
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
                Log.Error($"|Internationalization.GetTranslation|No Translation for key {key}");
                return $"No Translation for key {key}";
            }
        }

        private void UpdatePluginMetadataTranslations()
        {
            foreach (var p in PluginManager.GetPluginsForInterface<IPluginI18n>())
            {
                var pluginI18N = p.Plugin as IPluginI18n;
                if (pluginI18N == null) return;
                try
                {
                    p.Metadata.Name = pluginI18N.GetTranslatedPluginTitle();
                    p.Metadata.Description = pluginI18N.GetTranslatedPluginDescription();
                }
                catch (Exception e)
                {
                    Log.Exception($"|Internationalization.UpdatePluginMetadataTranslations|Failed for <{p.Metadata.Name}>", e);
                }
            }
        }

        public string LanguageFile(string folder, string language)
        {
            if (Directory.Exists(folder))
            {
                string path = Path.Combine(folder, language);
                if (File.Exists(path))
                {
                    return path;
                }
                else
                {
                    Log.Error($"|Internationalization.LanguageFile|Language path can't be found <{path}>");
                    string english = Path.Combine(folder, DefaultFile);
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