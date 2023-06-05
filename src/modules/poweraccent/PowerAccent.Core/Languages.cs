// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using PowerToys.PowerAccentKeyboardService;

namespace PowerAccent.Core
{
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
        EST,
        FR,
        HR,
        HE,
        HU,
        IS,
        IT,
        KU,
        LT,
        MK,
        MI,
        NL,
        NO,
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

    internal sealed class Languages
    {
        public static string[] GetDefaultLetterKey(LetterKey letter, Language lang)
        {
            return lang switch
            {
                Language.ALL => GetDefaultLetterKeyALL(letter), // ALL
                Language.CA => GetDefaultLetterKeyCA(letter), // Catalan
                Language.CUR => GetDefaultLetterKeyCUR(letter), // Currency
                Language.CY => GetDefaultLetterKeyCY(letter), // Welsh
                Language.CZ => GetDefaultLetterKeyCZ(letter), // Czech
                Language.GA => GetDefaultLetterKeyGA(letter), // Gaeilge (Irish)
                Language.GD => GetDefaultLetterKeyGD(letter), // Gàidhlig (Scottish Gaelic)
                Language.DE => GetDefaultLetterKeyDE(letter), // German
                Language.EST => GetDefaultLetterKeyEST(letter), // Estonian
                Language.FR => GetDefaultLetterKeyFR(letter), // French
                Language.HR => GetDefaultLetterKeyHR(letter), // Croatian
                Language.HE => GetDefaultLetterKeyHE(letter), // Hebrew
                Language.HU => GetDefaultLetterKeyHU(letter), // Hungarian
                Language.IS => GetDefaultLetterKeyIS(letter), // Iceland
                Language.IT => GetDefaultLetterKeyIT(letter), // Italian
                Language.KU => GetDefaultLetterKeyKU(letter), // Kurdish
                Language.LT => GetDefaultLetterKeyLT(letter), // Lithuanian
                Language.MK => GetDefaultLetterKeyMK(letter), // Macedonian
                Language.MI => GetDefaultLetterKeyMI(letter), // Maori
                Language.NL => GetDefaultLetterKeyNL(letter), // Dutch
                Language.NO => GetDefaultLetterKeyNO(letter), // Norwegian
                Language.PI => GetDefaultLetterKeyPI(letter), // Pinyin
                Language.PL => GetDefaultLetterKeyPL(letter), // Polish
                Language.PT => GetDefaultLetterKeyPT(letter), // Portuguese
                Language.RO => GetDefaultLetterKeyRO(letter), // Romanian
                Language.SK => GetDefaultLetterKeySK(letter), // Slovak
                Language.SP => GetDefaultLetterKeySP(letter), // Spain
                Language.SR => GetDefaultLetterKeySR(letter), // Serbian
                Language.SV => GetDefaultLetterKeySV(letter), // Swedish
                Language.TK => GetDefaultLetterKeyTK(letter), // Turkish
                _ => throw new ArgumentException("The language {0} is not know in this context", lang.ToString()),
            };
        }

        // All
        private static string[] GetDefaultLetterKeyALL(LetterKey letter)
        {
            return letter switch
            {
                LetterKey.VK_0 => new string[] { "₀", "⁰" },
                LetterKey.VK_1 => new string[] { "₁", "¹" },
                LetterKey.VK_2 => new string[] { "₂", "²" },
                LetterKey.VK_3 => new string[] { "₃", "³" },
                LetterKey.VK_4 => new string[] { "₄", "⁴" },
                LetterKey.VK_5 => new string[] { "₅", "⁵" },
                LetterKey.VK_6 => new string[] { "₆", "⁶" },
                LetterKey.VK_7 => new string[] { "₇", "⁷" },
                LetterKey.VK_8 => new string[] { "₈", "⁸" },
                LetterKey.VK_9 => new string[] { "₉", "⁹" },
                LetterKey.VK_A => new string[] { "á", "à", "ä", "â", "ă", "å", "α", "ā", "ą", "ȧ", "ã", "ǎ", "æ" },
                LetterKey.VK_B => new string[] { "ḃ", "β" },
                LetterKey.VK_C => new string[] { "ç", "ć", "ĉ", "č", "ċ", "¢", "χ" },
                LetterKey.VK_D => new string[] { "ď", "ḋ", "đ", "δ", "ð" },
                LetterKey.VK_E => new string[] { "é", "è", "ê", "ë", "ě", "ē", "ę", "ė", "ε", "η", "€" },
                LetterKey.VK_F => new string[] { "ƒ", "ḟ" },
                LetterKey.VK_G => new string[] { "ğ", "ģ", "ǧ", "ġ", "ĝ", "ǥ", "γ" },
                LetterKey.VK_H => new string[] { "ḣ", "ĥ", "ħ" },
                LetterKey.VK_I => new string[] { "ï", "î", "í", "ì", "ī", "į", "ı", "İ", "ι", "ǐ" },
                LetterKey.VK_J => new string[] { "ĵ" },
                LetterKey.VK_K => new string[] { "ķ", "ǩ", "κ" },
                LetterKey.VK_L => new string[] { "ĺ", "ľ", "ļ", "ł", "₺", "λ" },
                LetterKey.VK_M => new string[] { "ṁ", "μ" },
                LetterKey.VK_N => new string[] { "ñ", "ń", "ŋ", "ň", "ņ", "ṅ", "ⁿ", "ν" },
                LetterKey.VK_O => new string[] { "ô", "ó", "ö", "ő", "ò", "ō", "ȯ", "ø", "õ", "œ", "ω", "ο", "ǒ" },
                LetterKey.VK_P => new string[] { "ṗ", "₽", "π", "φ", "ψ" },
                LetterKey.VK_R => new string[] { "ŕ", "ř", "ṙ", "₹", "ρ" },
                LetterKey.VK_S => new string[] { "ś", "ş", "š", "ș", "ṡ", "ŝ", "ß", "σ", "$" },
                LetterKey.VK_T => new string[] { "ţ", "ť", "ț", "ṫ", "ŧ", "θ", "τ", "þ" },
                LetterKey.VK_U => new string[] { "û", "ú", "ü", "ŭ", "ű", "ù", "ů", "ū", "ų", "ǔ", "υ", "ǖ", "ǘ", "ǚ", "ǜ" },
                LetterKey.VK_W => new string[] { "ẇ", "ŵ", "₩" },
                LetterKey.VK_X => new string[] { "ẋ", "ξ", "×" },
                LetterKey.VK_Y => new string[] { "ÿ", "ŷ", "ý", "ẏ" },
                LetterKey.VK_Z => new string[] { "ź", "ž", "ż", "ʒ", "ǯ", "ζ" },
                LetterKey.VK_COMMA => new string[] { "¿", "¡", "∙", "₋", "⁻", "–", "≤", "≥", "≠", "≈", "≙", "±", "₊", "⁺" },
                LetterKey.VK_PERIOD => new string[] { "\u0300", "\u0301", "\u0302", "\u0303", "\u0304", "\u0308", "\u030C" },
                LetterKey.VK_MINUS => new string[] { "~", "‐", "‑", "‒", "–", "—", "―", "⁓", "−", "⸺", "⸻" },
                LetterKey.VK_SLASH_ => new string[] { "÷" },
                LetterKey.VK_DIVIDE_ => new string[] { "÷" },
                LetterKey.VK_MULTIPLY_ => new string[] { "×", "⋅" },
                _ => Array.Empty<string>(),
            };
        }

        // Currencies (source: https://www.eurochange.co.uk/travel-money/world-currency-abbreviations-symbols-and-codes-travel-money)
        private static string[] GetDefaultLetterKeyCUR(LetterKey letter)
        {
            return letter switch
            {
                LetterKey.VK_B => new string[] { "฿", "в" },
                LetterKey.VK_C => new string[] { "¢", "₡", "č" },
                LetterKey.VK_D => new string[] { "₫" },
                LetterKey.VK_E => new string[] { "€" },
                LetterKey.VK_F => new string[] { "ƒ" },
                LetterKey.VK_H => new string[] { "₴" },
                LetterKey.VK_K => new string[] { "₭" },
                LetterKey.VK_L => new string[] { "ł" },
                LetterKey.VK_N => new string[] { "л" },
                LetterKey.VK_M => new string[] { "₼" },
                LetterKey.VK_P => new string[] { "£", "₽" },
                LetterKey.VK_R => new string[] { "₹", "៛", "﷼" },
                LetterKey.VK_S => new string[] { "$", "₪" },
                LetterKey.VK_T => new string[] { "₮", "₺" },
                LetterKey.VK_W => new string[] { "₩" },
                LetterKey.VK_Y => new string[] { "¥" },
                LetterKey.VK_Z => new string[] { "z" },
                _ => Array.Empty<string>(),
            };
        }

        // Croatian
        private static string[] GetDefaultLetterKeyHR(LetterKey letter)
        {
            return letter switch
            {
                LetterKey.VK_C => new string[] { "ć", "č" },
                LetterKey.VK_D => new string[] { "đ" },
                LetterKey.VK_S => new string[] { "š" },
                LetterKey.VK_Z => new string[] { "ž" },
                _ => Array.Empty<string>(),
            };
        }

        // Estonian
        private static string[] GetDefaultLetterKeyEST(LetterKey letter)
        {
            return letter switch
            {
                LetterKey.VK_A => new string[] { "ä" },
                LetterKey.VK_E => new string[] { "€" },
                LetterKey.VK_O => new string[] { "ö", "õ" },
                LetterKey.VK_U => new string[] { "ü" },
                LetterKey.VK_Z => new string[] { "ž" },
                LetterKey.VK_S => new string[] { "š" },
                _ => Array.Empty<string>(),
            };
        }

        // French
        private static string[] GetDefaultLetterKeyFR(LetterKey letter)
        {
            return letter switch
            {
                LetterKey.VK_A => new string[] { "à", "â", "á", "ä", "ã", "æ" },
                LetterKey.VK_C => new string[] { "ç" },
                LetterKey.VK_E => new string[] { "é", "è", "ê", "ë", "€" },
                LetterKey.VK_I => new string[] { "î", "ï", "í", "ì" },
                LetterKey.VK_O => new string[] { "ô", "ö", "ó", "ò", "õ", "œ" },
                LetterKey.VK_U => new string[] { "û", "ù", "ü", "ú" },
                LetterKey.VK_Y => new string[] { "ÿ", "ý" },
                _ => Array.Empty<string>(),
            };
        }

        // Iceland
        private static string[] GetDefaultLetterKeyIS(LetterKey letter)
        {
            return letter switch
            {
                LetterKey.VK_A => new string[] { "á", "æ" },
                LetterKey.VK_D => new string[] { "ð" },
                LetterKey.VK_E => new string[] { "é" },
                LetterKey.VK_O => new string[] { "ó", "ö" },
                LetterKey.VK_U => new string[] { "ú" },
                LetterKey.VK_Y => new string[] { "ý" },
                LetterKey.VK_T => new string[] { "þ" },
                _ => Array.Empty<string>(),
            };
        }

        // Spain
        private static string[] GetDefaultLetterKeySP(LetterKey letter)
        {
            return letter switch
            {
                LetterKey.VK_A => new string[] { "á" },
                LetterKey.VK_E => new string[] { "é", "€" },
                LetterKey.VK_I => new string[] { "í" },
                LetterKey.VK_N => new string[] { "ñ" },
                LetterKey.VK_O => new string[] { "ó" },
                LetterKey.VK_U => new string[] { "ú", "ü" },
                LetterKey.VK_COMMA => new string[] { "¿", "?" },
                _ => Array.Empty<string>(),
            };
        }

        // Catalan
        private static string[] GetDefaultLetterKeyCA(LetterKey letter)
        {
            return letter switch
            {
                LetterKey.VK_A => new string[] { "à", "á" },
                LetterKey.VK_C => new string[] { "ç" },
                LetterKey.VK_E => new string[] { "è", "é", "€" },
                LetterKey.VK_I => new string[] { "ì", "í", "ï" },
                LetterKey.VK_N => new string[] { "ñ" },
                LetterKey.VK_O => new string[] { "ò", "ó" },
                LetterKey.VK_U => new string[] { "ù", "ú", "ü" },
                LetterKey.VK_L => new string[] { "·" },
                LetterKey.VK_COMMA => new string[] { "¿", "?" },
                _ => Array.Empty<string>(),
            };
        }

        // Maori
        private static string[] GetDefaultLetterKeyMI(LetterKey letter)
        {
            return letter switch
            {
                LetterKey.VK_A => new string[] { "ā" },
                LetterKey.VK_E => new string[] { "ē" },
                LetterKey.VK_I => new string[] { "ī" },
                LetterKey.VK_O => new string[] { "ō" },
                LetterKey.VK_S => new string[] { "$" },
                LetterKey.VK_U => new string[] { "ū" },
                _ => Array.Empty<string>(),
            };
        }

        // Dutch
        private static string[] GetDefaultLetterKeyNL(LetterKey letter)
        {
            return letter switch
            {
                LetterKey.VK_A => new string[] { "á", "à", "ä" },
                LetterKey.VK_C => new string[] { "ç" },
                LetterKey.VK_E => new string[] { "é", "è", "ë", "ê", "€" },
                LetterKey.VK_I => new string[] { "í", "ï", "î" },
                LetterKey.VK_N => new string[] { "ñ" },
                LetterKey.VK_O => new string[] { "ó", "ö", "ô" },
                LetterKey.VK_U => new string[] { "ú", "ü", "û" },
                _ => Array.Empty<string>(),
            };
        }

        // Pinyin
        private static string[] GetDefaultLetterKeyPI(LetterKey letter)
        {
            return letter switch
            {
                LetterKey.VK_1 => new string[] { "\u0304", "ˉ" },
                LetterKey.VK_2 => new string[] { "\u0301", "ˊ" },
                LetterKey.VK_3 => new string[] { "\u030c", "ˇ" },
                LetterKey.VK_4 => new string[] { "\u0300", "ˋ" },
                LetterKey.VK_5 => new string[] { "·" },
                LetterKey.VK_A => new string[] { "ā", "á", "ǎ", "à", "ɑ", "ɑ\u0304", "ɑ\u0301", "ɑ\u030c", "ɑ\u0300" },
                LetterKey.VK_C => new string[] { "ĉ" },
                LetterKey.VK_E => new string[] { "ē", "é", "ě", "è", "ê", "ê\u0304", "ế", "ê\u030c", "ề" },
                LetterKey.VK_I => new string[] { "ī", "í", "ǐ", "ì" },
                LetterKey.VK_M => new string[] { "m\u0304", "ḿ", "m\u030c", "m\u0300" },
                LetterKey.VK_N => new string[] { "n\u0304", "ń", "ň", "ǹ", "ŋ", "ŋ\u0304", "ŋ\u0301", "ŋ\u030c", "ŋ\u0300" },
                LetterKey.VK_O => new string[] { "ō", "ó", "ǒ", "ò" },
                LetterKey.VK_S => new string[] { "ŝ" },
                LetterKey.VK_U => new string[] { "ū", "ú", "ǔ", "ù", "ü", "ǖ", "ǘ", "ǚ", "ǜ" },
                LetterKey.VK_V => new string[] { "ü", "ǖ", "ǘ", "ǚ", "ǜ" },
                LetterKey.VK_Y => new string[] { "¥" },
                LetterKey.VK_Z => new string[] { "ẑ" },
                _ => Array.Empty<string>(),
            };
        }

        // Turkish
        private static string[] GetDefaultLetterKeyTK(LetterKey letter)
        {
            return letter switch
            {
                LetterKey.VK_A => new string[] { "â" },
                LetterKey.VK_C => new string[] { "ç" },
                LetterKey.VK_E => new string[] { "ë", "€" },
                LetterKey.VK_G => new string[] { "ğ" },
                LetterKey.VK_I => new string[] { "ı", "İ", "î", },
                LetterKey.VK_O => new string[] { "ö", "ô" },
                LetterKey.VK_S => new string[] { "ş" },
                LetterKey.VK_T => new string[] { "₺" },
                LetterKey.VK_U => new string[] { "ü", "û" },
                _ => Array.Empty<string>(),
            };
        }

        // Polish
        private static string[] GetDefaultLetterKeyPL(LetterKey letter)
        {
            return letter switch
            {
                LetterKey.VK_A => new string[] { "ą" },
                LetterKey.VK_C => new string[] { "ć" },
                LetterKey.VK_E => new string[] { "ę", "€" },
                LetterKey.VK_L => new string[] { "ł" },
                LetterKey.VK_N => new string[] { "ń" },
                LetterKey.VK_O => new string[] { "ó" },
                LetterKey.VK_S => new string[] { "ś" },
                LetterKey.VK_Z => new string[] { "ż", "ź" },
                _ => Array.Empty<string>(),
            };
        }

        // Portuguese
        private static string[] GetDefaultLetterKeyPT(LetterKey letter)
        {
            return letter switch
            {
                LetterKey.VK_0 => new string[] { "₀", "⁰" },
                LetterKey.VK_1 => new string[] { "₁", "¹" },
                LetterKey.VK_2 => new string[] { "₂", "²" },
                LetterKey.VK_3 => new string[] { "₃", "³" },
                LetterKey.VK_4 => new string[] { "₄", "⁴" },
                LetterKey.VK_5 => new string[] { "₅", "⁵" },
                LetterKey.VK_6 => new string[] { "₆", "⁶" },
                LetterKey.VK_7 => new string[] { "₇", "⁷" },
                LetterKey.VK_8 => new string[] { "₈", "⁸" },
                LetterKey.VK_9 => new string[] { "₉", "⁹" },
                LetterKey.VK_A => new string[] { "á", "à", "â", "ã" },
                LetterKey.VK_C => new string[] { "ç" },
                LetterKey.VK_E => new string[] { "é", "ê", "€" },
                LetterKey.VK_I => new string[] { "í" },
                LetterKey.VK_O => new string[] { "ô", "ó", "õ" },
                LetterKey.VK_P => new string[] { "π" },
                LetterKey.VK_S => new string[] { "$" },
                LetterKey.VK_U => new string[] { "ú" },
                LetterKey.VK_COMMA => new string[] { "≤", "≥", "≠", "≈", "≙", "±", "₊", "⁺" },
                _ => Array.Empty<string>(),
            };
        }

        // Slovak
        private static string[] GetDefaultLetterKeySK(LetterKey letter)
        {
            return letter switch
            {
                LetterKey.VK_A => new string[] { "á", "ä" },
                LetterKey.VK_C => new string[] { "č" },
                LetterKey.VK_D => new string[] { "ď" },
                LetterKey.VK_E => new string[] { "é", "€" },
                LetterKey.VK_I => new string[] { "í" },
                LetterKey.VK_L => new string[] { "ľ", "ĺ" },
                LetterKey.VK_N => new string[] { "ň" },
                LetterKey.VK_O => new string[] { "ó", "ô" },
                LetterKey.VK_R => new string[] { "ŕ" },
                LetterKey.VK_S => new string[] { "š" },
                LetterKey.VK_T => new string[] { "ť" },
                LetterKey.VK_U => new string[] { "ú" },
                LetterKey.VK_Y => new string[] { "ý" },
                LetterKey.VK_Z => new string[] { "ž" },
                _ => Array.Empty<string>(),
            };
        }

        // Gaeilge (Irish language)
        private static string[] GetDefaultLetterKeyGA(LetterKey letter)
        {
            return letter switch
            {
                LetterKey.VK_A => new string[] { "á" },
                LetterKey.VK_E => new string[] { "é" },
                LetterKey.VK_I => new string[] { "í" },
                LetterKey.VK_O => new string[] { "ó" },
                LetterKey.VK_U => new string[] { "ú" },
                _ => Array.Empty<string>(),
            };
        }

        // Gàidhlig (Scottish Gaelic)
        private static string[] GetDefaultLetterKeyGD(LetterKey letter)
        {
            return letter switch
            {
                LetterKey.VK_A => new string[] { "à" },
                LetterKey.VK_E => new string[] { "è" },
                LetterKey.VK_I => new string[] { "ì" },
                LetterKey.VK_O => new string[] { "ò" },
                LetterKey.VK_U => new string[] { "ù" },
                _ => Array.Empty<string>(),
            };
        }

        // Czech
        private static string[] GetDefaultLetterKeyCZ(LetterKey letter)
        {
            return letter switch
            {
                LetterKey.VK_A => new string[] { "á" },
                LetterKey.VK_C => new string[] { "č" },
                LetterKey.VK_D => new string[] { "ď" },
                LetterKey.VK_E => new string[] { "ě", "é" },
                LetterKey.VK_I => new string[] { "í" },
                LetterKey.VK_N => new string[] { "ň" },
                LetterKey.VK_O => new string[] { "ó" },
                LetterKey.VK_R => new string[] { "ř" },
                LetterKey.VK_S => new string[] { "š" },
                LetterKey.VK_T => new string[] { "ť" },
                LetterKey.VK_U => new string[] { "ů", "ú" },
                LetterKey.VK_Y => new string[] { "ý" },
                LetterKey.VK_Z => new string[] { "ž" },
                _ => Array.Empty<string>(),
            };
        }

        // German
        private static string[] GetDefaultLetterKeyDE(LetterKey letter)
        {
            return letter switch
            {
                LetterKey.VK_A => new string[] { "ä" },
                LetterKey.VK_E => new string[] { "€" },
                LetterKey.VK_O => new string[] { "ö" },
                LetterKey.VK_S => new string[] { "ß" },
                LetterKey.VK_U => new string[] { "ü" },
                _ => Array.Empty<string>(),
            };
        }

        // Hebrew
        private static string[] GetDefaultLetterKeyHE(LetterKey letter)
        {
            return letter switch
            {
                LetterKey.VK_A => new string[] { "שׂ", "שׁ", "\u05b0" },
                LetterKey.VK_B => new string[] { "׆" },
                LetterKey.VK_E => new string[] { "\u05b8", "\u05b3", "\u05bb" },
                LetterKey.VK_G => new string[] { "ױ" },
                LetterKey.VK_H => new string[] { "ײ", "ײַ", "ׯ", "\u05b4" },
                LetterKey.VK_M => new string[] { "\u05b5" },
                LetterKey.VK_P => new string[] { "\u05b7", "\u05b2" },
                LetterKey.VK_S => new string[] { "\u05bc" },
                LetterKey.VK_T => new string[] { "ﭏ" },
                LetterKey.VK_U => new string[] { "וֹ", "וּ", "װ", "\u05b9" },
                LetterKey.VK_X => new string[] { "\u05b6", "\u05b1" },
                LetterKey.VK_Y => new string[] { "ױ" },
                LetterKey.VK_COMMA => new string[] { "”", "’", "״", "׳" },
                LetterKey.VK_PERIOD => new string[] { "\u05ab", "\u05bd", "\u05bf" },
                LetterKey.VK_MINUS => new string[] { "–", "־" },
                _ => Array.Empty<string>(),
            };
        }

        // Hungarian
        private static string[] GetDefaultLetterKeyHU(LetterKey letter)
        {
            return letter switch
            {
                LetterKey.VK_A => new string[] { "á" },
                LetterKey.VK_E => new string[] { "é" },
                LetterKey.VK_I => new string[] { "í" },
                LetterKey.VK_O => new string[] { "ó", "ő", "ö" },
                LetterKey.VK_U => new string[] { "ú", "ű", "ü" },
                _ => Array.Empty<string>(),
            };
        }

        // Romanian
        private static string[] GetDefaultLetterKeyRO(LetterKey letter)
        {
            return letter switch
            {
                LetterKey.VK_A => new string[] { "ă", "â" },
                LetterKey.VK_I => new string[] { "î" },
                LetterKey.VK_S => new string[] { "ș" },
                LetterKey.VK_T => new string[] { "ț" },
                _ => Array.Empty<string>(),
            };
        }

        // Italian
        private static string[] GetDefaultLetterKeyIT(LetterKey letter)
        {
            return letter switch
            {
                LetterKey.VK_A => new string[] { "à" },
                LetterKey.VK_E => new string[] { "è", "é", "€" },
                LetterKey.VK_I => new string[] { "ì", "í" },
                LetterKey.VK_O => new string[] { "ò", "ó" },
                LetterKey.VK_U => new string[] { "ù", "ú" },
                _ => Array.Empty<string>(),
            };
        }

        // Kurdish
        private static string[] GetDefaultLetterKeyKU(LetterKey letter)
        {
            return letter switch
            {
                LetterKey.VK_C => new string[] { "ç" },
                LetterKey.VK_E => new string[] { "ê", "€" },
                LetterKey.VK_I => new string[] { "î" },
                LetterKey.VK_O => new string[] { "ö", "ô" },
                LetterKey.VK_L => new string[] { "ł" },
                LetterKey.VK_N => new string[] { "ň" },
                LetterKey.VK_R => new string[] { "ř" },
                LetterKey.VK_S => new string[] { "ş" },
                LetterKey.VK_U => new string[] { "û", "ü" },
                _ => Array.Empty<string>(),
            };
        }

        // Welsh
        private static string[] GetDefaultLetterKeyCY(LetterKey letter)
        {
            return letter switch
            {
                LetterKey.VK_A => new string[] { "â" },
                LetterKey.VK_E => new string[] { "ê" },
                LetterKey.VK_I => new string[] { "î" },
                LetterKey.VK_O => new string[] { "ô" },
                LetterKey.VK_U => new string[] { "û" },
                LetterKey.VK_Y => new string[] { "ŷ" },
                LetterKey.VK_W => new string[] { "ŵ" },
                _ => Array.Empty<string>(),
            };
        }

        // Swedish
        private static string[] GetDefaultLetterKeySV(LetterKey letter)
        {
            return letter switch
            {
                LetterKey.VK_A => new string[] { "å", "ä" },
                LetterKey.VK_O => new string[] { "ö" },
                _ => Array.Empty<string>(),
            };
        }

        // Serbian
        private static string[] GetDefaultLetterKeySR(LetterKey letter)
        {
            return letter switch
            {
                LetterKey.VK_C => new string[] { "ć", "č" },
                LetterKey.VK_D => new string[] { "đ" },
                LetterKey.VK_S => new string[] { "š" },
                LetterKey.VK_Z => new string[] { "ž" },
                _ => Array.Empty<string>(),
            };
        }

        // Macedonian
        private static string[] GetDefaultLetterKeyMK(LetterKey letter)
        {
            return letter switch
            {
                LetterKey.VK_E => new string[] { "ѐ" },
                LetterKey.VK_I => new string[] { "ѝ" },
                _ => Array.Empty<string>(),
            };
        }

        // Norwegian
        private static string[] GetDefaultLetterKeyNO(LetterKey letter)
        {
            return letter switch
            {
                LetterKey.VK_A => new string[] { "å", "æ" },
                LetterKey.VK_E => new string[] { "€" },
                LetterKey.VK_O => new string[] { "ø" },
                LetterKey.VK_S => new string[] { "$" },
                _ => Array.Empty<string>(),
            };
        }

        // Lithuanian
        private static string[] GetDefaultLetterKeyLT(LetterKey letter)
        {
            return letter switch
            {
                LetterKey.VK_A => new string[] { "ą" },
                LetterKey.VK_C => new string[] { "č" },
                LetterKey.VK_E => new string[] { "ę", "ė", "€" },
                LetterKey.VK_I => new string[] { "į" },
                LetterKey.VK_S => new string[] { "š" },
                LetterKey.VK_U => new string[] { "ų", "ū" },
                LetterKey.VK_Z => new string[] { "ž" },
                _ => Array.Empty<string>(),
            };
        }
    }
}
