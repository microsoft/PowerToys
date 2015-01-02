using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using Wox.Core.UI;
using Wox.Infrastructure.Logger;
using Wox.Infrastructure.Storage.UserSettings;

namespace Wox.Core.i18n
{
    public class InternationalizationManager : IUIResource
    {
        private static List<string> languageDirectories = new List<string>();
        private static InternationalizationManager instance;
        private static object syncObject = new object();

        private InternationalizationManager() { }

        public static InternationalizationManager Instance
        {
            get
            {
                if (instance == null)
                {
                    lock (syncObject)
                    {
                        if (instance == null)
                        {
                            instance = new InternationalizationManager();
                        }
                    }
                }
                return instance;
            }
        }

        static InternationalizationManager()
        {
            languageDirectories.Add(Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "Languages"));

            string userProfilePath = Environment.GetEnvironmentVariable("USERPROFILE");
            if (userProfilePath != null)
            {
                languageDirectories.Add(Path.Combine(Path.Combine(userProfilePath, ".Wox"), "Languages"));
            }

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

        public void ChangeLanguage(string name)
        {
            string path = GetLanguagePath(name);
            if (string.IsNullOrEmpty(path))
            {
                path = GetLanguagePath("English");
                if (string.IsNullOrEmpty(path))
                {
                    throw new System.Exception("Change Language failed");
                }
            }

            UserSettingStorage.Instance.Language = name;
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

        public List<string> LoadAvailableLanguages()
        {
            List<string> themes = new List<string>();
            foreach (var directory in languageDirectories)
            {
                themes.AddRange(
                    Directory.GetFiles(directory)
                        .Where(filePath => filePath.EndsWith(".xaml"))
                        .Select(Path.GetFileNameWithoutExtension)
                        .ToList());
            }
            return themes;
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

        private string GetLanguagePath(string name)
        {
            foreach (string directory in languageDirectories)
            {
                string path = Path.Combine(directory, name + ".xaml");
                if (File.Exists(path))
                {
                    return path;
                }
            }

            return string.Empty;
        }
    }
}