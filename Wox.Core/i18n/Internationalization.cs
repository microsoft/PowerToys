using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows;
using Wox.Core.Exception;
using Wox.Core.UI;
using Wox.Core.UserSettings;
using Wox.Infrastructure.Logger;

namespace Wox.Core.i18n
{
    public class Internationalization : IInternationalization, IUIResource
    {
        private static string DefaultLanguageDirectory = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "Languages");

        static Internationalization()
        {
            MakesureThemeDirectoriesExist();
        }

        private static void MakesureThemeDirectoriesExist()
        {
            if (!Directory.Exists(DefaultLanguageDirectory))
            {
                try
                {
                    Directory.CreateDirectory(DefaultLanguageDirectory);
                }
                catch (System.Exception e)
                {
                    Log.Error(e.Message);
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
                Source = new Uri(GetLanguageFile(DefaultLanguageDirectory), UriKind.Absolute)
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
            string path = Path.Combine(DefaultLanguageDirectory, language.LanguageCode + ".xaml");
            if (File.Exists(path))
            {
                return path;
            }

            return string.Empty;
        }


        public string GetLanguageFile(string folder)
        {
            if (!Directory.Exists(folder)) return string.Empty;

            string path = Path.Combine(folder, UserSettingStorage.Instance.Language + ".xaml");
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
                else
                {
                    string file = Directory.GetFiles(folder).FirstOrDefault(o => o.EndsWith("xaml"));
                    if (string.IsNullOrEmpty(file))
                    {
                        throw new WoxI18nException(string.Format("Couldn't find language file from:{0}, current selected language:{1}"));
                    }

                    return Path.Combine(folder, file);
                }
            }

        }
    }
}
