using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Wox.Core.i18n
{
    interface IInternationalization
    {
        List<Language> LoadAvailableLanguages();

        string GetTranslation(string key);

        /// <summary>
        /// Get language file for current user selected language
        /// if couldn't find the current selected language file, it will first try to load en.xaml 
        /// if en.xaml couldn't find, return empty string
        /// </summary>
        /// <param name="folder"></param>
        /// <returns></returns>
        string GetLanguageFile(string folder);

        void ChangeLanguage(Language language);

        void ChangeLanguage(string languageCode);
    }
}
