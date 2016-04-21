using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using Wox.Core.UserSettings;
using Wox.Infrastructure.Exception;
using Wox.Infrastructure.Logger;
using Wox.Plugin;

namespace Wox.Core.Resource
{
    public class Internationalization : Resource
    {
        public UserSettings.Settings Settings { get; set; }

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
                    Log.Error(e);
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
            Language language = AvailableLanguages.GetAvailableLanguages().FirstOrDefault(o => o.LanguageCode.ToLower() == languageCode.ToLower());
            if (language == null)
            {
                throw new WoxI18nException("Invalid language code:" + languageCode);
            }

            return language;
        }

        public void ChangeLanguage(Language language)
        {
            if (language == null) throw new WoxI18nException("language can't be null");

            string path = GetLanguagePath(language);
            if (string.IsNullOrEmpty(path))
            {
                path = GetLanguagePath(AvailableLanguages.English);
                if (string.IsNullOrEmpty(path))
                {
                    throw new Exception("Change Language failed");
                }
            }

            Settings.Language = language.LanguageCode;
            ResourceMerger.UpdateResource(this);
        }



        public override ResourceDictionary GetResourceDictionary()
        {
            var dictionary = new ResourceDictionary
            {
                Source = new Uri(GetLanguageFile(DirectoryPath), UriKind.Absolute)
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
                var woxPluginException = new WoxPluginException(pluginPair.Metadata.Name, "Update Plugin metadata translation failed:", e);
                Log.Error(woxPluginException);
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
