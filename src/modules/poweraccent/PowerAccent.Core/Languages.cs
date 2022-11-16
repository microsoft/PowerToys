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
        CUR,
        CY,
        CZ,
        DE,
        FR,
        HR,
        HU,
        IS,
        IT,
        MI,
        NL,
        PI,
        PL,
        PT,
        RO,
        SK,
        SP,
        SV,
        TK,
    }

    internal class Languages
    {
        public static char[] GetDefaultLetterKey(LetterKey letter, Language lang)
        {
            switch (lang)
            {
                case Language.ALL: return GetDefaultLetterKeyALL(letter); // ALL
                case Language.CUR: return GetDefaultLetterKeyCUR(letter); // Currency
                case Language.CY: return GetDefaultLetterKeyCY(letter); // Welsh
                case Language.CZ: return GetDefaultLetterKeyCZ(letter); // Czech
                case Language.DE: return GetDefaultLetterKeyDE(letter); // German
                case Language.FR: return GetDefaultLetterKeyFR(letter); // French
                case Language.HR: return GetDefaultLetterKeyHR(letter); // Croatian
                case Language.HU: return GetDefaultLetterKeyHU(letter); // Hungarian
                case Language.IS: return GetDefaultLetterKeyIS(letter); // Iceland
                case Language.IT: return GetDefaultLetterKeyIT(letter); // Italian
                case Language.MI: return GetDefaultLetterKeyMI(letter); // Maori
                case Language.NL: return GetDefaultLetterKeyNL(letter); // Dutch
                case Language.PI: return GetDefaultLetterKeyPI(letter); // Pinyin
                case Language.PL: return GetDefaultLetterKeyPL(letter); // Polish
                case Language.PT: return GetDefaultLetterKeyPT(letter); // Portuguese
                case Language.RO: return GetDefaultLetterKeyRO(letter); // Romanian
                case Language.SK: return GetDefaultLetterKeySK(letter); // Slovak
                case Language.SP: return GetDefaultLetterKeySP(letter); // Spain
                case Language.SV: return GetDefaultLetterKeySV(letter); // Swedish
                case Language.TK: return GetDefaultLetterKeyTK(letter); // Turkish
            }

            throw new ArgumentException("The language {0} is not know in this context", lang.ToString());
        }

        // All
        private static char[] GetDefaultLetterKeyALL(LetterKey letter)
        {
            switch (letter)
            {
                case LetterKey.VK_0:
                    return new char[] { '₀', '⁰' };
                case LetterKey.VK_1:
                    return new char[] { '₁', '¹' };
                case LetterKey.VK_2:
                    return new char[] { '₂', '²' };
                case LetterKey.VK_3:
                    return new char[] { '₃', '³' };
                case LetterKey.VK_4:
                    return new char[] { '₄', '⁴' };
                case LetterKey.VK_5:
                    return new char[] { '₅', '⁵' };
                case LetterKey.VK_6:
                    return new char[] { '₆', '⁶' };
                case LetterKey.VK_7:
                    return new char[] { '₇', '⁷' };
                case LetterKey.VK_8:
                    return new char[] { '₈', '⁸' };
                case LetterKey.VK_9:
                    return new char[] { '₉', '⁹' };
                case LetterKey.VK_A:
                    return new char[] { 'á', 'à', 'ä', 'â', 'ă', 'å', 'α', 'ā', 'ą', 'ȧ', 'ã', 'æ' };
                case LetterKey.VK_B:
                    return new char[] { 'ḃ', 'β' };
                case LetterKey.VK_C:
                    return new char[] { 'ç', 'ć', 'ĉ', 'č', 'ċ', '¢', 'χ' };
                case LetterKey.VK_D:
                    return new char[] { 'ď', 'ḋ', 'đ', 'δ', 'ð' };
                case LetterKey.VK_E:
                    return new char[] { 'é', 'è', 'ê', 'ë', 'ě', 'ē', 'ę', 'ė', 'ε', 'η', '€' };
                case LetterKey.VK_F:
                    return new char[] { 'ƒ', 'ḟ' };
                case LetterKey.VK_G:
                    return new char[] { 'ğ', 'ģ', 'ǧ', 'ġ', 'ĝ', 'ǥ', 'γ' };
                case LetterKey.VK_H:
                    return new char[] { 'ḣ', 'ĥ', 'ħ' };
                case LetterKey.VK_I:
                    return new char[] { 'ï', 'î', 'í', 'ì', 'ī', 'į', 'i', 'ı', 'İ', 'ι' };
                case LetterKey.VK_J:
                    return new char[] { 'ĵ' };
                case LetterKey.VK_K:
                    return new char[] { 'ķ', 'ǩ', 'κ' };
                case LetterKey.VK_L:
                    return new char[] { 'ĺ', 'ľ', 'ļ', 'ł', '₺', 'λ' };
                case LetterKey.VK_M:
                    return new char[] { 'ṁ', 'μ' };
                case LetterKey.VK_N:
                    return new char[] { 'ñ', 'ń', 'ŋ', 'ň', 'ņ', 'ṅ', 'ⁿ', 'ν' };
                case LetterKey.VK_O:
                    return new char[] { 'ô', 'ó', 'ö', 'ő', 'ò', 'ō', 'ȯ', 'ø', 'õ', 'œ', 'ω', 'ο' };
                case LetterKey.VK_P:
                    return new char[] { 'ṗ', '₽', 'π', 'φ', 'ψ' };
                case LetterKey.VK_R:
                    return new char[] { 'ŕ', 'ř', 'ṙ', '₹', 'ρ' };
                case LetterKey.VK_S:
                    return new char[] { 'ś', 'ş', 'š', 'ș', 'ṡ', 'ŝ', 'ß', 'σ', '$' };
                case LetterKey.VK_T:
                    return new char[] { 'ţ', 'ť', 'ț', 'ṫ', 'ŧ', 'θ', 'τ', 'þ' };
                case LetterKey.VK_U:
                    return new char[] { 'û', 'ú', 'ü', 'ŭ', 'ű', 'ù', 'ů', 'ū', 'ų', 'υ' };
                case LetterKey.VK_W:
                    return new char[] { 'ẇ', 'ŵ', '₩' };
                case LetterKey.VK_X:
                    return new char[] { 'ẋ', 'ξ' };
                case LetterKey.VK_Y:
                    return new char[] { 'ÿ', 'ŷ', 'ý', 'ẏ' };
                case LetterKey.VK_Z:
                    return new char[] { 'ź', 'ž', 'ż', 'ʒ', 'ǯ', 'ζ' };
                case LetterKey.VK_COMMA:
                    return new char[] { '¿', '¡', '∙', '₋', '⁻', '–', '≤', '≥', '≠', '≈', '≙', '±', '₊', '⁺' };
            }

            return Array.Empty<char>();
        }

        // Currencies (source: https://www.eurochange.co.uk/travel-money/world-currency-abbreviations-symbols-and-codes-travel-money)
        private static char[] GetDefaultLetterKeyCUR(LetterKey letter)
        {
            switch (letter)
            {
                case LetterKey.VK_B:
                    return new char[] { '฿', 'в' };
                case LetterKey.VK_C:
                    return new char[] { '¢', '₡', 'č' };
                case LetterKey.VK_D:
                    return new char[] { '₫' };
                case LetterKey.VK_E:
                    return new char[] { '€' };
                case LetterKey.VK_F:
                    return new char[] { 'ƒ' };
                case LetterKey.VK_H:
                    return new char[] { '₴' };
                case LetterKey.VK_K:
                    return new char[] { '₭' };
                case LetterKey.VK_L:
                    return new char[] { 'ł' };
                case LetterKey.VK_N:
                    return new char[] { 'л' };
                case LetterKey.VK_M:
                    return new char[] { '₼' };
                case LetterKey.VK_P:
                    return new char[] { '£', '₽' };
                case LetterKey.VK_R:
                    return new char[] { '₹', '៛', '﷼' };
                case LetterKey.VK_S:
                    return new char[] { '$', '₪' };
                case LetterKey.VK_T:
                    return new char[] { '₮', '₺' };
                case LetterKey.VK_W:
                    return new char[] { '₩' };
                case LetterKey.VK_Y:
                    return new char[] { '¥' };
                case LetterKey.VK_Z:
                    return new char[] { 'z' };
            }

            return Array.Empty<char>();
        }

        // Croatian
        private static char[] GetDefaultLetterKeyHR(LetterKey letter)
        {
            switch (letter)
            {
                case LetterKey.VK_C:
                    return new char[] { 'ć', 'č' };
                case LetterKey.VK_D:
                    return new char[] { 'đ' };
                case LetterKey.VK_S:
                    return new char[] { 'š' };
                case LetterKey.VK_Z:
                    return new char[] { 'ž' };
            }

            return Array.Empty<char>();
        }

        // French
        private static char[] GetDefaultLetterKeyFR(LetterKey letter)
        {
            switch (letter)
            {
                case LetterKey.VK_A:
                    return new char[] { 'à', 'â', 'á', 'ä', 'ã', 'æ' };
                case LetterKey.VK_C:
                    return new char[] { 'ç' };
                case LetterKey.VK_E:
                    return new char[] { 'é', 'è', 'ê', 'ë', '€' };
                case LetterKey.VK_I:
                    return new char[] { 'î', 'ï', 'í', 'ì' };
                case LetterKey.VK_O:
                    return new char[] { 'ô', 'ö', 'ó', 'ò', 'õ', 'œ' };
                case LetterKey.VK_U:
                    return new char[] { 'û', 'ù', 'ü', 'ú' };
                case LetterKey.VK_Y:
                    return new char[] { 'ÿ', 'ý' };
            }

            return Array.Empty<char>();
        }

        // Iceland
        private static char[] GetDefaultLetterKeyIS(LetterKey letter)
        {
            switch (letter)
            {
                case LetterKey.VK_A:
                    return new char[] { 'á', 'æ' };
                case LetterKey.VK_D:
                    return new char[] { 'ð' };
                case LetterKey.VK_E:
                    return new char[] { 'é' };
                case LetterKey.VK_O:
                    return new char[] { 'ó', 'ö' };
                case LetterKey.VK_U:
                    return new char[] { 'ú' };
                case LetterKey.VK_Y:
                    return new char[] { 'ý' };
                case LetterKey.VK_T:
                    return new char[] { 'þ' };
            }

            return Array.Empty<char>();
        }

        // Spain
        private static char[] GetDefaultLetterKeySP(LetterKey letter)
        {
            switch (letter)
            {
                case LetterKey.VK_A:
                    return new char[] { 'á' };
                case LetterKey.VK_E:
                    return new char[] { 'é', '€' };
                case LetterKey.VK_I:
                    return new char[] { 'í' };
                case LetterKey.VK_N:
                    return new char[] { 'ñ' };
                case LetterKey.VK_O:
                    return new char[] { 'ó' };
                case LetterKey.VK_U:
                    return new char[] { 'ú', 'ü' };
                case LetterKey.VK_COMMA:
                    return new char[] { '¿', '?' };
            }

            return Array.Empty<char>();
        }

        // Maori
        private static char[] GetDefaultLetterKeyMI(LetterKey letter)
        {
            switch (letter)
            {
                case LetterKey.VK_A:
                    return new char[] { 'ā' };
                case LetterKey.VK_E:
                    return new char[] { 'ē' };
                case LetterKey.VK_I:
                    return new char[] { 'ī' };
                case LetterKey.VK_O:
                    return new char[] { 'ō' };
                case LetterKey.VK_S:
                    return new char[] { '$' };
                case LetterKey.VK_U:
                    return new char[] { 'ū' };
            }

            return Array.Empty<char>();
        }

        // Dutch
        private static char[] GetDefaultLetterKeyNL(LetterKey letter)
        {
            switch (letter)
            {
                case LetterKey.VK_A:
                    return new char[] { 'á', 'à', 'ä' };
                case LetterKey.VK_C:
                    return new char[] { 'ç' };
                case LetterKey.VK_E:
                    return new char[] { 'é', 'è', 'ë', 'ê', '€' };
                case LetterKey.VK_I:
                    return new char[] { 'í', 'ï', 'î' };
                case LetterKey.VK_N:
                    return new char[] { 'ñ' };
                case LetterKey.VK_O:
                    return new char[] { 'ó', 'ö', 'ô' };
                case LetterKey.VK_U:
                    return new char[] { 'ú', 'ü', 'û' };
            }

            return Array.Empty<char>();
        }

        // Pinyin
        private static char[] GetDefaultLetterKeyPI(LetterKey letter)
        {
            switch (letter)
            {
                case LetterKey.VK_A:
                    return new char[] { 'ā', 'á', 'ǎ', 'à', 'a' };
                case LetterKey.VK_E:
                    return new char[] { 'ē', 'é', 'ě', 'è', 'e' };
                case LetterKey.VK_I:
                    return new char[] { 'ī', 'í', 'ǐ', 'ì', 'i' };
                case LetterKey.VK_O:
                    return new char[] { 'ō', 'ó', 'ǒ', 'ò', 'o' };
                case LetterKey.VK_U:
                    return new char[] { 'ū', 'ú', 'ǔ', 'ù', 'u' };
                case LetterKey.VK_V:
                    return new char[] { 'ǖ', 'ǘ', 'ǚ', 'ǜ', 'ü' };
            }

            return Array.Empty<char>();
        }

        // Turkish
        private static char[] GetDefaultLetterKeyTK(LetterKey letter)
        {
            switch (letter)
            {
                case LetterKey.VK_A:
                    return new char[] { 'â' };
                case LetterKey.VK_C:
                    return new char[] { 'ç' };
                case LetterKey.VK_E:
                    return new char[] { 'ë', '€' };
                case LetterKey.VK_G:
                    return new char[] { 'ğ' };
                case LetterKey.VK_I:
                    return new char[] { 'ı', 'İ', 'î', };
                case LetterKey.VK_O:
                    return new char[] { 'ö', 'ô' };
                case LetterKey.VK_S:
                    return new char[] { 'ş' };
                case LetterKey.VK_T:
                    return new char[] { '₺' };
                case LetterKey.VK_U:
                    return new char[] { 'ü', 'û' };
            }

            return Array.Empty<char>();
        }

        // Polish
        private static char[] GetDefaultLetterKeyPL(LetterKey letter)
        {
            switch (letter)
            {
                case LetterKey.VK_A:
                    return new char[] { 'ą' };
                case LetterKey.VK_C:
                    return new char[] { 'ć' };
                case LetterKey.VK_E:
                    return new char[] { 'ę', '€' };
                case LetterKey.VK_L:
                    return new char[] { 'ł' };
                case LetterKey.VK_N:
                    return new char[] { 'ń' };
                case LetterKey.VK_O:
                    return new char[] { 'ó' };
                case LetterKey.VK_S:
                    return new char[] { 'ś' };
                case LetterKey.VK_Z:
                    return new char[] { 'ż', 'ź' };
            }

            return Array.Empty<char>();
        }

        // Portuguese
        private static char[] GetDefaultLetterKeyPT(LetterKey letter)
        {
            switch (letter)
            {
                case LetterKey.VK_0:
                    return new char[] { '₀', '⁰' };
                case LetterKey.VK_1:
                    return new char[] { '₁', '¹' };
                case LetterKey.VK_2:
                    return new char[] { '₂', '²' };
                case LetterKey.VK_3:
                    return new char[] { '₃', '³' };
                case LetterKey.VK_4:
                    return new char[] { '₄', '⁴' };
                case LetterKey.VK_5:
                    return new char[] { '₅', '⁵' };
                case LetterKey.VK_6:
                    return new char[] { '₆', '⁶' };
                case LetterKey.VK_7:
                    return new char[] { '₇', '⁷' };
                case LetterKey.VK_8:
                    return new char[] { '₈', '⁸' };
                case LetterKey.VK_9:
                    return new char[] { '₉', '⁹' };
                case LetterKey.VK_A:
                    return new char[] { 'á', 'à', 'â', 'ã' };
                case LetterKey.VK_C:
                    return new char[] { 'ç' };
                case LetterKey.VK_E:
                    return new char[] { 'é', 'ê', '€' };
                case LetterKey.VK_I:
                    return new char[] { 'í' };
                case LetterKey.VK_O:
                    return new char[] { 'ô', 'ó', 'õ' };
                case LetterKey.VK_P:
                    return new char[] { 'π' };
                case LetterKey.VK_S:
                    return new char[] { '$' };
                case LetterKey.VK_U:
                    return new char[] { 'ú' };
                case LetterKey.VK_COMMA:
                    return new char[] { '≤', '≥', '≠', '≈', '≙', '±', '₊', '⁺' };
            }

            return Array.Empty<char>();
        }

        // Slovak
        private static char[] GetDefaultLetterKeySK(LetterKey letter)
        {
            switch (letter)
            {
                case LetterKey.VK_A:
                    return new char[] { 'á', 'ä' };
                case LetterKey.VK_C:
                    return new char[] { 'č' };
                case LetterKey.VK_D:
                    return new char[] { 'ď' };
                case LetterKey.VK_E:
                    return new char[] { 'é', '€' };
                case LetterKey.VK_I:
                    return new char[] { 'í' };
                case LetterKey.VK_L:
                    return new char[] { 'ľ', 'ĺ' };
                case LetterKey.VK_N:
                    return new char[] { 'ň' };
                case LetterKey.VK_O:
                    return new char[] { 'ó', 'ô' };
                case LetterKey.VK_R:
                    return new char[] { 'ŕ' };
                case LetterKey.VK_S:
                    return new char[] { 'š' };
                case LetterKey.VK_T:
                    return new char[] { 'ť' };
                case LetterKey.VK_U:
                    return new char[] { 'ú' };
                case LetterKey.VK_Y:
                    return new char[] { 'ý' };
                case LetterKey.VK_Z:
                    return new char[] { 'ž' };
            }

            return Array.Empty<char>();
        }

        // Czech
        private static char[] GetDefaultLetterKeyCZ(LetterKey letter)
        {
            switch (letter)
            {
                case LetterKey.VK_A:
                    return new char[] { 'á' };
                case LetterKey.VK_C:
                    return new char[] { 'č' };
                case LetterKey.VK_D:
                    return new char[] { 'ď' };
                case LetterKey.VK_E:
                    return new char[] { 'ě', 'é' };
                case LetterKey.VK_I:
                    return new char[] { 'í' };
                case LetterKey.VK_N:
                    return new char[] { 'ň' };
                case LetterKey.VK_O:
                    return new char[] { 'ó' };
                case LetterKey.VK_R:
                    return new char[] { 'ř' };
                case LetterKey.VK_S:
                    return new char[] { 'š' };
                case LetterKey.VK_T:
                    return new char[] { 'ť' };
                case LetterKey.VK_U:
                    return new char[] { 'ů', 'ú' };
                case LetterKey.VK_Y:
                    return new char[] { 'ý' };
                case LetterKey.VK_Z:
                    return new char[] { 'ž' };
            }

            return Array.Empty<char>();
        }

        // German
        private static char[] GetDefaultLetterKeyDE(LetterKey letter)
        {
            switch (letter)
            {
                case LetterKey.VK_A:
                    return new char[] { 'ä' };
                case LetterKey.VK_E:
                    return new char[] { '€' };
                case LetterKey.VK_O:
                    return new char[] { 'ö' };
                case LetterKey.VK_S:
                    return new char[] { 'ß' };
                case LetterKey.VK_U:
                    return new char[] { 'ü' };
            }

            return Array.Empty<char>();
        }

        // Hungarian
        private static char[] GetDefaultLetterKeyHU(LetterKey letter)
        {
            switch (letter)
            {
                case LetterKey.VK_A:
                    return new char[] { 'á' };
                case LetterKey.VK_E:
                    return new char[] { 'é' };
                case LetterKey.VK_I:
                    return new char[] { 'í' };
                case LetterKey.VK_O:
                    return new char[] { 'ó', 'ő', 'ö' };
                case LetterKey.VK_U:
                    return new char[] { 'ú', 'ű', 'ü' };
            }

            return Array.Empty<char>();
        }

        // Romanian
        private static char[] GetDefaultLetterKeyRO(LetterKey letter)
        {
            switch (letter)
            {
                case LetterKey.VK_A:
                    return new char[] { 'ă', 'â' };
                case LetterKey.VK_I:
                    return new char[] { 'î' };
                case LetterKey.VK_S:
                    return new char[] { 'ș' };
                case LetterKey.VK_T:
                    return new char[] { 'ț' };
            }

            return Array.Empty<char>();
        }

        // Italian
        private static char[] GetDefaultLetterKeyIT(LetterKey letter)
        {
            switch (letter)
            {
                case LetterKey.VK_A:
                    return new char[] { 'à' };
                case LetterKey.VK_E:
                    return new char[] { 'è', 'é', '€' };
                case LetterKey.VK_I:
                    return new char[] { 'ì', 'í' };
                case LetterKey.VK_O:
                    return new char[] { 'ò', 'ó' };
                case LetterKey.VK_U:
                    return new char[] { 'ù', 'ú' };
            }

            return Array.Empty<char>();
        }

        // Welsh
        private static char[] GetDefaultLetterKeyCY(LetterKey letter)
        {
            switch (letter)
            {
                case LetterKey.VK_A:
                    return new char[] { 'â' };
                case LetterKey.VK_E:
                    return new char[] { 'ê' };
                case LetterKey.VK_I:
                    return new char[] { 'î' };
                case LetterKey.VK_O:
                    return new char[] { 'ô' };
                case LetterKey.VK_U:
                    return new char[] { 'û' };
                case LetterKey.VK_Y:
                    return new char[] { 'ŷ' };
            }

            return Array.Empty<char>();
        }

        // Swedish
        private static char[] GetDefaultLetterKeySV(LetterKey letter)
        {
            switch (letter)
            {
                case LetterKey.VK_A:
                    return new char[] { 'å', 'ä' };
                case LetterKey.VK_O:
                    return new char[] { 'ö' };
            }

            return Array.Empty<char>();
        }
    }
}
