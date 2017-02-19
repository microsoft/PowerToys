using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using Wox.Infrastructure.Exception;
using Wox.Infrastructure.Logger;
using Wox.Infrastructure.UserSettings;
using Wox.Plugin;

namespace Wox.Core.Resource
{
    public class Internationalization : Resource
    {
        public Settings Settings { get; set; }

        public Internationalization()
        {
            DirectoryName = "Languages";
            MakesureDirectoriesExist();
        }

        private void MakesureDirectoriesExist()
        {
            if (!Directory.Exists(DirectoryPath))
            {
                try
                {
                    Directory.CreateDirectory(DirectoryPath);
                }
                catch (Exception e)
                {
                    Log.Exception($"|Internationalization.MakesureDirectoriesExist|Exception when create directory <{DirectoryPath}>", e);
                }
            }
        }

        public void ChangeLanguage(string languageCode)
        {
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
            if (language != null)
            {
                string path = GetLanguagePath(language);
                if (!string.IsNullOrEmpty(path))
                {
                    Settings.Language = language.LanguageCode;
                    ResourceMerger.UpdateResource(this);
                }
                else
                {
                    Log.Error($"|Internationalization.ChangeLanguage|Language path can't be found <{path}>");
                    path = GetLanguagePath(AvailableLanguages.English);
                    if (string.IsNullOrEmpty(path))
                    {
                        Log.Error($"|Internationalization.ChangeLanguage|Default english language path can't be found <{path}>");
                    }
                }
            }
            else
            {
                Log.Error("|Internationalization.ChangeLanguage|Language can't be null");
            }
        }



        public override ResourceDictionary GetResourceDictionary()
        {
            var uri = GetLanguageFile(DirectoryPath);
            var dictionary = new ResourceDictionary
            {
                Source = new Uri(uri, UriKind.Absolute)
            };
            return dictionary;
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

        private string GetLanguagePath(string languageCode)
        {
            Language language = GetLanguageByLanguageCode(languageCode);
            return GetLanguagePath(language);
        }


        internal void UpdatePluginMetadataTranslations(PluginPair pluginPair)
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

        private string GetLanguagePath(Language language)
        {
            string path = Path.Combine(DirectoryPath, language.LanguageCode + ".xaml");
            if (File.Exists(path))
            {
                return path;
            }

            return string.Empty;
        }


        public string GetLanguageFile(string folder)
        {
            if (!Directory.Exists(folder)) return string.Empty;

            string path = Path.Combine(folder, Settings.Language + ".xaml");
            if (File.Exists(path))
            {
                return path;
            }
            else
            {
                string english = Path.Combine(folder, "en.xaml");
                if (File.Exists(english))
                {
                    return english;
                }

                return string.Empty;
            }

        }
    }

}
