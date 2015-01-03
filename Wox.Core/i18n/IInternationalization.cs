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

        void ChangeLanguage(Language language);

        void ChangeLanguage(string languageCode);
    }
}
