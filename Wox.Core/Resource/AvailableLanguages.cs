using System.Collections.Generic;

namespace Wox.Core.Resource
{
    internal static class AvailableLanguages
    {
        public static Language English = new Language("en", "English");
        public static Language Chinese = new Language("zh-cn", "中文");
        public static Language Chinese_TW = new Language("zh-tw", "中文（繁体）");
        public static Language Ukrainian = new Language("uk-UA", "Українська");
        public static Language Russian = new Language("ru", "Русский");
        public static Language French = new Language("fr", "Français");
        public static Language Japanese = new Language("ja", "日本語");
        public static Language Dutch = new Language("nl", "Dutch");
        public static Language Polish = new Language("pl", "Polski");
        public static Language Danish = new Language("da", "Dansk");
        public static Language German = new Language("de", "Deutsch");
        public static Language Korean = new Language("ko", "한국어");
        public static Language Serbian = new Language("sr", "Srpski");
        public static Language Portuguese_BR = new Language("pt-br", "Português (Brasil)");
		public static Language Italian = new Language("it", "Italiano");
        public static Language Norwegian_Bokmal = new Language("nb-NO", "Norsk Bokmål");
	public static Language Slovak = new Language("sk", "Slovenský");

        public static List<Language> GetAvailableLanguages()
        {
            List<Language> languages = new List<Language>
            {
                English,
                Chinese,
                Chinese_TW,
                Ukrainian,
                Russian,
                French,
                Japanese,
                Dutch,
                Polish,
                Danish,
                German,
                Korean,
                Serbian,
                Portuguese_BR,
				Italian,
                Norwegian_Bokmal
            };
            return languages;
        }
    }
}
