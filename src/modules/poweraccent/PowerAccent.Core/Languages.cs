// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace PowerAccent.Core
{
    using System;
    using PowerToys.PowerAccentKeyboardService;

    public enum Language
    {
        ALL,
        CA,
        CUR,
        CY,
        CZ,
        GA,
        GD,
        DE,
        FR,
        HR,
        HU,
        IS,
        IT,
        KU,
        MI,
        NL,
        PI,
        PL,
        PT,
        RO,
        SK,
        SP,
        SR,
        SV,
        TK,
    }

    internal class Languages
    {
        public static string[] GetDefaultLetterKey(LetterKey letter, Language lang)
        {
            switch (lang)
            {
                case Language.ALL: return GetDefaultLetterKeyALL(letter); // ALL
                case Language.CA: return GetDefaultLetterKeyCA(letter); // Catalan
                case Language.CUR: return GetDefaultLetterKeyCUR(letter); // Currency
                case Language.CY: return GetDefaultLetterKeyCY(letter); // Welsh
                case Language.CZ: return GetDefaultLetterKeyCZ(letter); // Czech
                case Language.GA: return GetDefaultLetterKeyGA(letter); // Gaeilge (Irish Gaelic)
                case Language.GD: return GetDefaultLetterKeyGD(letter); // Gàidhlig (Scottish Gaelic)
                case Language.DE: return GetDefaultLetterKeyDE(letter); // German
                case Language.FR: return GetDefaultLetterKeyFR(letter); // French
                case Language.HR: return GetDefaultLetterKeyHR(letter); // Croatian
                case Language.HU: return GetDefaultLetterKeyHU(letter); // Hungarian
                case Language.IS: return GetDefaultLetterKeyIS(letter); // Iceland
                case Language.IT: return GetDefaultLetterKeyIT(letter); // Italian
                case Language.KU: return GetDefaultLetterKeyKU(letter); // Kurdish
                case Language.MI: return GetDefaultLetterKeyMI(letter); // Maori
                case Language.NL: return GetDefaultLetterKeyNL(letter); // Dutch
                case Language.PI: return GetDefaultLetterKeyPI(letter); // Pinyin
                case Language.PL: return GetDefaultLetterKeyPL(letter); // Polish
                case Language.PT: return GetDefaultLetterKeyPT(letter); // Portuguese
                case Language.RO: return GetDefaultLetterKeyRO(letter); // Romanian
                case Language.SK: return GetDefaultLetterKeySK(letter); // Slovak
                case Language.SP: return GetDefaultLetterKeySP(letter); // Spain
                case Language.SR: return GetDefaultLetterKeySR(letter); // Serbian
                case Language.SV: return GetDefaultLetterKeySV(letter); // Swedish
                case Language.TK: return GetDefaultLetterKeyTK(letter); // Turkish
            }

            throw new ArgumentException("The language {0} is not know in this context", lang.ToString());
        }

        // All
        private static string[] GetDefaultLetterKeyALL(LetterKey letter)
        {
            switch (letter)
            {
                case LetterKey.VK_0:
                    return new string[] { "₀", "⁰" };
                case LetterKey.VK_1:
                    return new string[] { "₁", "¹" };
                case LetterKey.VK_2:
                    return new string[] { "₂", "²" };
                case LetterKey.VK_3:
                    return new string[] { "₃", "³" };
                case LetterKey.VK_4:
                    return new string[] { "₄", "⁴" };
                case LetterKey.VK_5:
                    return new string[] { "₅", "⁵" };
                case LetterKey.VK_6:
                    return new string[] { "₆", "⁶" };
                case LetterKey.VK_7:
                    return new string[] { "₇", "⁷" };
                case LetterKey.VK_8:
                    return new string[] { "₈", "⁸" };
                case LetterKey.VK_9:
                    return new string[] { "₉", "⁹" };
                case LetterKey.VK_A:
                    return new string[] { "á", "à", "ä", "â", "ă", "å", "α", "ā", "ą", "ȧ", "ã", "æ" };
                case LetterKey.VK_B:
                    return new string[] { "ḃ", "β" };
                case LetterKey.VK_C:
                    return new string[] { "ç", "ć", "ĉ", "č", "ċ", "¢", "χ" };
                case LetterKey.VK_D:
                    return new string[] { "ď", "ḋ", "đ", "δ", "ð" };
                case LetterKey.VK_E:
                    return new string[] { "é", "è", "ê", "ë", "ě", "ē", "ę", "ė", "ε", "η", "€" };
                case LetterKey.VK_F:
                    return new string[] { "ƒ", "ḟ" };
                case LetterKey.VK_G:
                    return new string[] { "ğ", "ģ", "ǧ", "ġ", "ĝ", "ǥ", "γ" };
                case LetterKey.VK_H:
                    return new string[] { "ḣ", "ĥ", "ħ" };
                case LetterKey.VK_I:
                    return new string[] { "ï", "î", "í", "ì", "ī", "į", "i", "ı", "İ", "ι" };
                case LetterKey.VK_J:
                    return new string[] { "ĵ" };
                case LetterKey.VK_K:
                    return new string[] { "ķ", "ǩ", "κ" };
                case LetterKey.VK_L:
                    return new string[] { "ĺ", "ľ", "ļ", "ł", "₺", "λ" };
                case LetterKey.VK_M:
                    return new string[] { "ṁ", "μ" };
                case LetterKey.VK_N:
                    return new string[] { "ñ", "ń", "ŋ", "ň", "ņ", "ṅ", "ⁿ", "ν" };
                case LetterKey.VK_O:
                    return new string[] { "ô", "ó", "ö", "ő", "ò", "ō", "ȯ", "ø", "õ", "œ", "ω", "ο" };
                case LetterKey.VK_P:
                    return new string[] { "ṗ", "₽", "π", "φ", "ψ" };
                case LetterKey.VK_R:
                    return new string[] { "ŕ", "ř", "ṙ", "₹", "ρ" };
                case LetterKey.VK_S:
                    return new string[] { "ś", "ş", "š", "ș", "ṡ", "ŝ", "ß", "σ", "$" };
                case LetterKey.VK_T:
                    return new string[] { "ţ", "ť", "ț", "ṫ", "ŧ", "θ", "τ", "þ" };
                case LetterKey.VK_U:
                    return new string[] { "û", "ú", "ü", "ŭ", "ű", "ù", "ů", "ū", "ų", "υ" };
                case LetterKey.VK_W:
                    return new string[] { "ẇ", "ŵ", "₩" };
                case LetterKey.VK_X:
                    return new string[] { "ẋ", "ξ" };
                case LetterKey.VK_Y:
                    return new string[] { "ÿ", "ŷ", "ý", "ẏ" };
                case LetterKey.VK_Z:
                    return new string[] { "ź", "ž", "ż", "ʒ", "ǯ", "ζ" };
                case LetterKey.VK_COMMA:
                    return new string[] { "¿", "¡", "∙", "₋", "⁻", "–", "≤", "≥", "≠", "≈", "≙", "±", "₊", "⁺" };
            }

            return Array.Empty<string>();
        }

        // Currencies (source: https://www.eurochange.co.uk/travel-money/world-currency-abbreviations-symbols-and-codes-travel-money)
        private static string[] GetDefaultLetterKeyCUR(LetterKey letter)
        {
            switch (letter)
            {
                case LetterKey.VK_B:
                    return new string[] { "฿", "в" };
                case LetterKey.VK_C:
                    return new string[] { "¢", "₡", "č" };
                case LetterKey.VK_D:
                    return new string[] { "₫" };
                case LetterKey.VK_E:
                    return new string[] { "€" };
                case LetterKey.VK_F:
                    return new string[] { "ƒ" };
                case LetterKey.VK_H:
                    return new string[] { "₴" };
                case LetterKey.VK_K:
                    return new string[] { "₭" };
                case LetterKey.VK_L:
                    return new string[] { "ł" };
                case LetterKey.VK_N:
                    return new string[] { "л" };
                case LetterKey.VK_M:
                    return new string[] { "₼" };
                case LetterKey.VK_P:
                    return new string[] { "£", "₽" };
                case LetterKey.VK_R:
                    return new string[] { "₹", "៛", "﷼" };
                case LetterKey.VK_S:
                    return new string[] { "$", "₪" };
                case LetterKey.VK_T:
                    return new string[] { "₮", "₺" };
                case LetterKey.VK_W:
                    return new string[] { "₩" };
                case LetterKey.VK_Y:
                    return new string[] { "¥" };
                case LetterKey.VK_Z:
                    return new string[] { "z" };
            }

            return Array.Empty<string>();
        }

        // Croatian
        private static string[] GetDefaultLetterKeyHR(LetterKey letter)
        {
            switch (letter)
            {
                case LetterKey.VK_C:
                    return new string[] { "ć", "č" };
                case LetterKey.VK_D:
                    return new string[] { "đ" };
                case LetterKey.VK_S:
                    return new string[] { "š" };
                case LetterKey.VK_Z:
                    return new string[] { "ž" };
            }

            return Array.Empty<string>();
        }

        // French
        private static string[] GetDefaultLetterKeyFR(LetterKey letter)
        {
            switch (letter)
            {
                case LetterKey.VK_A:
                    return new string[] { "à", "â", "á", "ä", "ã", "æ" };
                case LetterKey.VK_C:
                    return new string[] { "ç" };
                case LetterKey.VK_E:
                    return new string[] { "é", "è", "ê", "ë", "€" };
                case LetterKey.VK_I:
                    return new string[] { "î", "ï", "í", "ì" };
                case LetterKey.VK_O:
                    return new string[] { "ô", "ö", "ó", "ò", "õ", "œ" };
                case LetterKey.VK_U:
                    return new string[] { "û", "ù", "ü", "ú" };
                case LetterKey.VK_Y:
                    return new string[] { "ÿ", "ý" };
            }

            return Array.Empty<string>();
        }

        // Iceland
        private static string[] GetDefaultLetterKeyIS(LetterKey letter)
        {
            switch (letter)
            {
                case LetterKey.VK_A:
                    return new string[] { "á", "æ" };
                case LetterKey.VK_D:
                    return new string[] { "ð" };
                case LetterKey.VK_E:
                    return new string[] { "é" };
                case LetterKey.VK_O:
                    return new string[] { "ó", "ö" };
                case LetterKey.VK_U:
                    return new string[] { "ú" };
                case LetterKey.VK_Y:
                    return new string[] { "ý" };
                case LetterKey.VK_T:
                    return new string[] { "þ" };
            }

            return Array.Empty<string>();
        }

        // Spain
        private static string[] GetDefaultLetterKeySP(LetterKey letter)
        {
            switch (letter)
            {
                case LetterKey.VK_A:
                    return new string[] { "á" };
                case LetterKey.VK_E:
                    return new string[] { "é", "€" };
                case LetterKey.VK_I:
                    return new string[] { "í" };
                case LetterKey.VK_N:
                    return new string[] { "ñ" };
                case LetterKey.VK_O:
                    return new string[] { "ó" };
                case LetterKey.VK_U:
                    return new string[] { "ú", "ü" };
                case LetterKey.VK_COMMA:
                    return new string[] { "¿", "?" };
            }

            return Array.Empty<string>();
        }

         // Catalan
        private static string[] GetDefaultLetterKeyCA(LetterKey letter)
        {
            switch (letter)
            {
                case LetterKey.VK_A:
                    return new string[] { "à", "á" };
                case LetterKey.VK_C:
                    return new string[] { "ç" };
                case LetterKey.VK_E:
                    return new string[] { "è", "é", "€" };
                case LetterKey.VK_I:
                    return new string[] { "ì", "í", "ï" };
                case LetterKey.VK_N:
                    return new string[] { "ñ" };
                case LetterKey.VK_O:
                    return new string[] { "ò", "ó" };
                case LetterKey.VK_U:
                    return new string[] { "ù", "ú", "ü" };
                case LetterKey.VK_L:
                    return new string[] { "·" };
                case LetterKey.VK_COMMA:
                    return new string[] { "¿", "?" };
            }

            return Array.Empty<string>();
        }

        // Maori
        private static string[] GetDefaultLetterKeyMI(LetterKey letter)
        {
            switch (letter)
            {
                case LetterKey.VK_A:
                    return new string[] { "ā" };
                case LetterKey.VK_E:
                    return new string[] { "ē" };
                case LetterKey.VK_I:
                    return new string[] { "ī" };
                case LetterKey.VK_O:
                    return new string[] { "ō" };
                case LetterKey.VK_S:
                    return new string[] { "$" };
                case LetterKey.VK_U:
                    return new string[] { "ū" };
            }

            return Array.Empty<string>();
        }

        // Dutch
        private static string[] GetDefaultLetterKeyNL(LetterKey letter)
        {
            switch (letter)
            {
                case LetterKey.VK_A:
                    return new string[] { "á", "à", "ä" };
                case LetterKey.VK_C:
                    return new string[] { "ç" };
                case LetterKey.VK_E:
                    return new string[] { "é", "è", "ë", "ê", "€" };
                case LetterKey.VK_I:
                    return new string[] { "í", "ï", "î" };
                case LetterKey.VK_N:
                    return new string[] { "ñ" };
                case LetterKey.VK_O:
                    return new string[] { "ó", "ö", "ô" };
                case LetterKey.VK_U:
                    return new string[] { "ú", "ü", "û" };
            }

            return Array.Empty<string>();
        }

        // Pinyin
        private static string[] GetDefaultLetterKeyPI(LetterKey letter)
        {
            switch (letter)
            {
                case LetterKey.VK_A:
                    return new string[] { "ā", "á", "ǎ", "à", "a", "ɑ̄", "ɑ́", "ɑ̌", "ɑ̀" };
                case LetterKey.VK_C:
                    return new string[] { "ĉ", "c" };
                case LetterKey.VK_E:
                    return new string[] { "ē", "é", "ě", "è", "e" };
                case LetterKey.VK_I:
                    return new string[] { "ī", "í", "ǐ", "ì", "i" };
                case LetterKey.VK_M:
                    return new string[] { "m̄", "ḿ", "m̌", "m̀", "m" };
                case LetterKey.VK_N:
                    return new string[] { "n̄", "ń", "ň", "ǹ", "n", "ŋ", "ŋ̄", "ŋ́", "ŋ̌", "ŋ̀" };
                case LetterKey.VK_O:
                    return new string[] { "ō", "ó", "ǒ", "ò", "o" };
                case LetterKey.VK_S:
                    return new string[] { "ŝ", "s" };
                case LetterKey.VK_U:
                    return new string[] { "ū", "ú", "ǔ", "ù", "u" };
                case LetterKey.VK_V:
                    return new string[] { "ǖ", "ǘ", "ǚ", "ǜ", "ü" };
                case LetterKey.VK_Y:
                    return new string[] { "¥", "y" };
                case LetterKey.VK_Z:
                    return new string[] { "ẑ", "z" };
            }

            return Array.Empty<string>();
        }

        // Turkish
        private static string[] GetDefaultLetterKeyTK(LetterKey letter)
        {
            switch (letter)
            {
                case LetterKey.VK_A:
                    return new string[] { "â" };
                case LetterKey.VK_C:
                    return new string[] { "ç" };
                case LetterKey.VK_E:
                    return new string[] { "ë", "€" };
                case LetterKey.VK_G:
                    return new string[] { "ğ" };
                case LetterKey.VK_I:
                    return new string[] { "ı", "İ", "î", };
                case LetterKey.VK_O:
                    return new string[] { "ö", "ô" };
                case LetterKey.VK_S:
                    return new string[] { "ş" };
                case LetterKey.VK_T:
                    return new string[] { "₺" };
                case LetterKey.VK_U:
                    return new string[] { "ü", "û" };
            }

            return Array.Empty<string>();
        }

        // Polish
        private static string[] GetDefaultLetterKeyPL(LetterKey letter)
        {
            switch (letter)
            {
                case LetterKey.VK_A:
                    return new string[] { "ą" };
                case LetterKey.VK_C:
                    return new string[] { "ć" };
                case LetterKey.VK_E:
                    return new string[] { "ę", "€" };
                case LetterKey.VK_L:
                    return new string[] { "ł" };
                case LetterKey.VK_N:
                    return new string[] { "ń" };
                case LetterKey.VK_O:
                    return new string[] { "ó" };
                case LetterKey.VK_S:
                    return new string[] { "ś" };
                case LetterKey.VK_Z:
                    return new string[] { "ż", "ź" };
            }

            return Array.Empty<string>();
        }

        // Portuguese
        private static string[] GetDefaultLetterKeyPT(LetterKey letter)
        {
            switch (letter)
            {
                case LetterKey.VK_0:
                    return new string[] { "₀", "⁰" };
                case LetterKey.VK_1:
                    return new string[] { "₁", "¹" };
                case LetterKey.VK_2:
                    return new string[] { "₂", "²" };
                case LetterKey.VK_3:
                    return new string[] { "₃", "³" };
                case LetterKey.VK_4:
                    return new string[] { "₄", "⁴" };
                case LetterKey.VK_5:
                    return new string[] { "₅", "⁵" };
                case LetterKey.VK_6:
                    return new string[] { "₆", "⁶" };
                case LetterKey.VK_7:
                    return new string[] { "₇", "⁷" };
                case LetterKey.VK_8:
                    return new string[] { "₈", "⁸" };
                case LetterKey.VK_9:
                    return new string[] { "₉", "⁹" };
                case LetterKey.VK_A:
                    return new string[] { "á", "à", "â", "ã" };
                case LetterKey.VK_C:
                    return new string[] { "ç" };
                case LetterKey.VK_E:
                    return new string[] { "é", "ê", "€" };
                case LetterKey.VK_I:
                    return new string[] { "í" };
                case LetterKey.VK_O:
                    return new string[] { "ô", "ó", "õ" };
                case LetterKey.VK_P:
                    return new string[] { "π" };
                case LetterKey.VK_S:
                    return new string[] { "$" };
                case LetterKey.VK_U:
                    return new string[] { "ú" };
                case LetterKey.VK_COMMA:
                    return new string[] { "≤", "≥", "≠", "≈", "≙", "±", "₊", "⁺" };
            }

            return Array.Empty<string>();
        }

        // Slovak
        private static string[] GetDefaultLetterKeySK(LetterKey letter)
        {
            switch (letter)
            {
                case LetterKey.VK_A:
                    return new string[] { "á", "ä" };
                case LetterKey.VK_C:
                    return new string[] { "č" };
                case LetterKey.VK_D:
                    return new string[] { "ď" };
                case LetterKey.VK_E:
                    return new string[] { "é", "€" };
                case LetterKey.VK_I:
                    return new string[] { "í" };
                case LetterKey.VK_L:
                    return new string[] { "ľ", "ĺ" };
                case LetterKey.VK_N:
                    return new string[] { "ň" };
                case LetterKey.VK_O:
                    return new string[] { "ó", "ô" };
                case LetterKey.VK_R:
                    return new string[] { "ŕ" };
                case LetterKey.VK_S:
                    return new string[] { "š" };
                case LetterKey.VK_T:
                    return new string[] { "ť" };
                case LetterKey.VK_U:
                    return new string[] { "ú" };
                case LetterKey.VK_Y:
                    return new string[] { "ý" };
                case LetterKey.VK_Z:
                    return new string[] { "ž" };
            }

            return Array.Empty<string>();
        }

        // Gaeilge (Irish language)
        private static string[] GetDefaultLetterKeyGA(LetterKey letter)
        {
            switch (letter)
            {
                case LetterKey.VK_A:
                    return new string[] { "á" };
                case LetterKey.VK_E:
                    return new string[] { "é" };
                case LetterKey.VK_I:
                    return new string[] { "í" };
                case LetterKey.VK_O:
                    return new string[] { "ó" };
                case LetterKey.VK_U:
                    return new string[] { "ú" };
            }

            return Array.Empty<string>();
        }

        // Gàidhlig (Scottish Gaelic)
        private static string[] GetDefaultLetterKeyGD(LetterKey letter)
        {
            switch (letter)
            {
                case LetterKey.VK_A:
                    return new string[] { "à" };
                case LetterKey.VK_E:
                    return new string[] { "è" };
                case LetterKey.VK_I:
                    return new string[] { "ì" };
                case LetterKey.VK_O:
                    return new string[] { "ò" };
                case LetterKey.VK_U:
                    return new string[] { "ù" };
            }

            return Array.Empty<string>();
        }

        // Czech
        private static string[] GetDefaultLetterKeyCZ(LetterKey letter)
        {
            switch (letter)
            {
                case LetterKey.VK_A:
                    return new string[] { "á" };
                case LetterKey.VK_C:
                    return new string[] { "č" };
                case LetterKey.VK_D:
                    return new string[] { "ď" };
                case LetterKey.VK_E:
                    return new string[] { "ě", "é" };
                case LetterKey.VK_I:
                    return new string[] { "í" };
                case LetterKey.VK_N:
                    return new string[] { "ň" };
                case LetterKey.VK_O:
                    return new string[] { "ó" };
                case LetterKey.VK_R:
                    return new string[] { "ř" };
                case LetterKey.VK_S:
                    return new string[] { "š" };
                case LetterKey.VK_T:
                    return new string[] { "ť" };
                case LetterKey.VK_U:
                    return new string[] { "ů", "ú" };
                case LetterKey.VK_Y:
                    return new string[] { "ý" };
                case LetterKey.VK_Z:
                    return new string[] { "ž" };
            }

            return Array.Empty<string>();
        }

        // German
        private static string[] GetDefaultLetterKeyDE(LetterKey letter)
        {
            switch (letter)
            {
                case LetterKey.VK_A:
                    return new string[] { "ä" };
                case LetterKey.VK_E:
                    return new string[] { "€" };
                case LetterKey.VK_O:
                    return new string[] { "ö" };
                case LetterKey.VK_S:
                    return new string[] { "ß" };
                case LetterKey.VK_U:
                    return new string[] { "ü" };
            }

            return Array.Empty<string>();
        }

        // Hungarian
        private static string[] GetDefaultLetterKeyHU(LetterKey letter)
        {
            switch (letter)
            {
                case LetterKey.VK_A:
                    return new string[] { "á" };
                case LetterKey.VK_E:
                    return new string[] { "é" };
                case LetterKey.VK_I:
                    return new string[] { "í" };
                case LetterKey.VK_O:
                    return new string[] { "ó", "ő", "ö" };
                case LetterKey.VK_U:
                    return new string[] { "ú", "ű", "ü" };
            }

            return Array.Empty<string>();
        }

        // Romanian
        private static string[] GetDefaultLetterKeyRO(LetterKey letter)
        {
            switch (letter)
            {
                case LetterKey.VK_A:
                    return new string[] { "ă", "â" };
                case LetterKey.VK_I:
                    return new string[] { "î" };
                case LetterKey.VK_S:
                    return new string[] { "ș" };
                case LetterKey.VK_T:
                    return new string[] { "ț" };
            }

            return Array.Empty<string>();
        }

        // Italian
        private static string[] GetDefaultLetterKeyIT(LetterKey letter)
        {
            switch (letter)
            {
                case LetterKey.VK_A:
                    return new string[] { "à" };
                case LetterKey.VK_E:
                    return new string[] { "è", "é", "€" };
                case LetterKey.VK_I:
                    return new string[] { "ì", "í" };
                case LetterKey.VK_O:
                    return new string[] { "ò", "ó" };
                case LetterKey.VK_U:
                    return new string[] { "ù", "ú" };
            }

            return Array.Empty<string>();
        }

        // Kurdish
        private static string[] GetDefaultLetterKeyKU(LetterKey letter)
        {
            switch (letter)
            {
                case LetterKey.VK_C:
                    return new string[] { "ç" };
                case LetterKey.VK_E:
                    return new string[] { "ê", "€" };
                case LetterKey.VK_I:
                    return new string[] { "î" };
                case LetterKey.VK_O:
                    return new string[] { "ö", "ô" };
                case LetterKey.VK_L:
                    return new string[] { "ł" };
                case LetterKey.VK_N:
                    return new string[] { "ň" };
                case LetterKey.VK_R:
                    return new string[] { "ř" };
                case LetterKey.VK_S:
                    return new string[] { "ş" };
                case LetterKey.VK_U:
                    return new string[] { "û", "ü" };
            }

            return Array.Empty<string>();
        }

        // Welsh
        private static string[] GetDefaultLetterKeyCY(LetterKey letter)
        {
            switch (letter)
            {
                case LetterKey.VK_A:
                    return new string[] { "â" };
                case LetterKey.VK_E:
                    return new string[] { "ê" };
                case LetterKey.VK_I:
                    return new string[] { "î" };
                case LetterKey.VK_O:
                    return new string[] { "ô" };
                case LetterKey.VK_U:
                    return new string[] { "û" };
                case LetterKey.VK_Y:
                    return new string[] { "ŷ" };
            }

            return Array.Empty<string>();
        }

        // Swedish
        private static string[] GetDefaultLetterKeySV(LetterKey letter)
        {
            switch (letter)
            {
                case LetterKey.VK_A:
                    return new string[] { "å", "ä" };
                case LetterKey.VK_O:
                    return new string[] { "ö" };
            }

            return Array.Empty<string>();
        }

        // Serbian
        private static string[] GetDefaultLetterKeySR(LetterKey letter)
        {
            switch (letter)
            {
                case LetterKey.VK_C:
                    return new string[] { "ć", "č" };
                case LetterKey.VK_D:
                    return new string[] { "đ" };
                case LetterKey.VK_S:
                    return new string[] { "š" };
                case LetterKey.VK_Z:
                    return new string[] { "ž" };
            }

            return Array.Empty<string>();
        }
    }
}
