// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace Wox.Core.Resource
{
    internal static class AvailableLanguages
    {
        public static Language English { get; set; } = new Language("en", "English");

        public static Language Chinese { get; set; } = new Language("zh-cn", "中文");

        public static Language Chinese_TW { get; set; } = new Language("zh-tw", "中文（繁体）");

        public static Language Ukrainian { get; set; } = new Language("uk-UA", "Українська");

        public static Language Russian { get; set; } = new Language("ru", "Русский");

        public static Language French { get; set; } = new Language("fr", "Français");

        public static Language Japanese { get; set; } = new Language("ja", "日本語");

        public static Language Dutch { get; set; } = new Language("nl", "Dutch");

        public static Language Polish { get; set; } = new Language("pl", "Polski");

        public static Language Danish { get; set; } = new Language("da", "Dansk");

        public static Language German { get; set; } = new Language("de", "Deutsch");

        public static Language Korean { get; set; } = new Language("ko", "한국어");

        public static Language Serbian { get; set; } = new Language("sr", "Srpski");

        public static Language Portuguese_BR { get; set; } = new Language("pt-br", "Português (Brasil)");

        public static Language Italian { get; set; } = new Language("it", "Italiano");

        public static Language Norwegian_Bokmal { get; set; } = new Language("nb-NO", "Norsk Bokmål");

        public static Language Slovak { get; set; } = new Language("sk", "Slovenský");

        public static Language Turkish { get; set; } = new Language("tr", "Türkçe");

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
                Norwegian_Bokmal,
                Slovak,
                Turkish,
            };
            return languages;
        }
    }
}
