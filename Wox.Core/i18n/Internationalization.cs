using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows;
using Wox.Core.Exception;
using Wox.Core.UI;
using Wox.Infrastructure.Logger;
using Wox.Infrastructure.Storage.UserSettings;

namespace Wox.Core.i18n
{
    public class Internationalization : IInternationalization, IUIResource
    {
        private static List<string> languageDirectories = new List<string>();

        static Internationalization()
        {
            languageDirectories.Add(Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "Languages"));
            MakesureThemeDirectoriesExist();
        }

        private static void MakesureThemeDirectoriesExist()
        {
            foreach (string pluginDirectory in languageDirectories)
            {
                if (!Directory.Exists(pluginDirectory))
                {
                    try
                    {
                        Directory.CreateDirectory(pluginDirectory);
                    }
                    catch (System.Exception e)
                    {
                        Log.Error(e.Message);
                    }
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
            if(language == null) throw new WoxI18nException("language can't be null");

            string path = GetLanguagePath(language);
            if (string.IsNullOrEmpty(path))
            {
                path = GetLanguagePath(AvailableLanguages.English);
                if (string.IsNullOrEmpty(path))
                {
                    throw new System.Exception("Change Language failed");
                }
            }

            UserSettingStorage.Instance.Language = language.LanguageCode;
            UserSettingStorage.Instance.Save();
            ResourceMerger.ApplyResources();
        }

        public ResourceDictionary GetResourceDictionary()
        {
            return new ResourceDictionary
            {
                Source = new Uri(GetLanguagePath(UserSettingStorage.Instance.Language), UriKind.Absolute)
            };
        }

        public List<Language> LoadAvailableLanguages()
        {
            return AvailableLanguages.GetAvailableLanguages();
        }

        public string GetTranslation(string key)
        {
            try
            {
                object translation = Application.Current.FindResource(key);
                if (translation == null)
                {
                    return "NoTranslation";
                }
                return translation.ToString();
            }
            catch
            {
                return "NoTranslation";
            }

        }

        private string GetLanguagePath(string languageCode)
        {
            Language language = GetLanguageByLanguageCode(languageCode);
            return GetLanguagePath(language);
        }

        private string GetLanguagePath(Language language)
        {
            foreach (string directory in languageDirectories)
            {
                string path = Path.Combine(directory, language.LanguageCode + ".xaml");
                if (File.Exists(path))
                {
                    return path;
                }
            }

            return string.Empty;
        }
    }
}
