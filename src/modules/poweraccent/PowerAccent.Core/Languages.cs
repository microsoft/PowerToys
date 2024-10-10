// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Concurrent;

using PowerToys.PowerAccentKeyboardService;

namespace PowerAccent.Core
{
    public enum Language
    {
        ALL,
        BG,
        CA,
        CRH,
        CUR,
        CY,
        CZ,
        DK,
        GA,
        GD,
        DE,
        EL,
        EST,
        EPO,
        FI,
        FR,
        HR,
        HE,
        HU,
        IS,
        IPA,
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
        ROM,
        SK,
        SL,
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
                Language.ALL => GetDefaultLetterKeyALL(letter), // All
                Language.BG => GetDefaultLetterKeyBG(letter), // Bulgarian
                Language.CA => GetDefaultLetterKeyCA(letter), // Catalan
                Language.CRH => GetDefaultLetterKeyCRH(letter), // Crimean Tatar
                Language.CUR => GetDefaultLetterKeyCUR(letter), // Currency
                Language.CY => GetDefaultLetterKeyCY(letter), // Welsh
                Language.CZ => GetDefaultLetterKeyCZ(letter), // Czech
                Language.DK => GetDefaultLetterKeyDK(letter), // Danish
                Language.GA => GetDefaultLetterKeyGA(letter), // Gaeilge (Irish)
                Language.GD => GetDefaultLetterKeyGD(letter), // Gàidhlig (Scottish Gaelic)
                Language.DE => GetDefaultLetterKeyDE(letter), // German
                Language.EL => GetDefaultLetterKeyEL(letter), // Greek
                Language.EST => GetDefaultLetterKeyEST(letter), // Estonian
                Language.EPO => GetDefaultLetterKeyEPO(letter), // Esperanto
                Language.FI => GetDefaultLetterKeyFI(letter), // Finnish
                Language.FR => GetDefaultLetterKeyFR(letter), // French
                Language.HR => GetDefaultLetterKeyHR(letter), // Croatian
                Language.HE => GetDefaultLetterKeyHE(letter), // Hebrew
                Language.HU => GetDefaultLetterKeyHU(letter), // Hungarian
                Language.IS => GetDefaultLetterKeyIS(letter), // Iceland
                Language.IPA => GetDefaultLetterKeyIPA(letter), // IPA (International phonetic alphabet)
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
                Language.ROM => GetDefaultLetterKeyROM(letter), // Middle Eastern Romanization
                Language.SK => GetDefaultLetterKeySK(letter), // Slovak
                Language.SL => GetDefaultLetterKeySL(letter), // Slovenian
                Language.SP => GetDefaultLetterKeySP(letter), // Spain
                Language.SR => GetDefaultLetterKeySR(letter), // Serbian
                Language.SV => GetDefaultLetterKeySV(letter), // Swedish
                Language.TK => GetDefaultLetterKeyTK(letter), // Turkish
                _ => throw new ArgumentException("The language {0} is not known in this context", lang.ToString()),
            };
        }

        // Store the computed letters for each key, so that subsequent calls don't take as long.
        private static ConcurrentDictionary<LetterKey, string[]> _allLanguagesCache = new ConcurrentDictionary<LetterKey, string[]>();

        // All
        private static string[] GetDefaultLetterKeyALL(LetterKey letter)
        {
            if (!_allLanguagesCache.TryGetValue(letter, out string[] cachedValue))
            {
                cachedValue = GetDefaultLetterKeyBG(letter)
                .Union(GetDefaultLetterKeyCA(letter))
                .Union(GetDefaultLetterKeyCRH(letter))
                .Union(GetDefaultLetterKeyCUR(letter))
                .Union(GetDefaultLetterKeyCY(letter))
                .Union(GetDefaultLetterKeyCZ(letter))
                .Union(GetDefaultLetterKeyDK(letter))
                .Union(GetDefaultLetterKeyGA(letter))
                .Union(GetDefaultLetterKeyGD(letter))
                .Union(GetDefaultLetterKeyDE(letter))
                .Union(GetDefaultLetterKeyEL(letter))
                .Union(GetDefaultLetterKeyEST(letter))
                .Union(GetDefaultLetterKeyEPO(letter))
                .Union(GetDefaultLetterKeyFI(letter))
                .Union(GetDefaultLetterKeyFR(letter))
                .Union(GetDefaultLetterKeyHR(letter))
                .Union(GetDefaultLetterKeyHE(letter))
                .Union(GetDefaultLetterKeyHU(letter))
                .Union(GetDefaultLetterKeyIS(letter))
                .Union(GetDefaultLetterKeyIPA(letter))
                .Union(GetDefaultLetterKeyIT(letter))
                .Union(GetDefaultLetterKeyKU(letter))
                .Union(GetDefaultLetterKeyLT(letter))
                .Union(GetDefaultLetterKeyROM(letter))
                .Union(GetDefaultLetterKeyMK(letter))
                .Union(GetDefaultLetterKeyMI(letter))
                .Union(GetDefaultLetterKeyNL(letter))
                .Union(GetDefaultLetterKeyNO(letter))
                .Union(GetDefaultLetterKeyPI(letter))
                .Union(GetDefaultLetterKeyPL(letter))
                .Union(GetDefaultLetterKeyPT(letter))
                .Union(GetDefaultLetterKeyRO(letter))
                .Union(GetDefaultLetterKeySK(letter))
                .Union(GetDefaultLetterKeySL(letter))
                .Union(GetDefaultLetterKeySP(letter))
                .Union(GetDefaultLetterKeySR(letter))
                .Union(GetDefaultLetterKeySV(letter))
                .Union(GetDefaultLetterKeyTK(letter))
                .Union(GetDefaultLetterKeyAllLanguagesOnly(letter))
                .ToArray();

                _allLanguagesCache[letter] = cachedValue;
            }

            return cachedValue;
        }

        // Contains all characters that should be shown in all languages but currently don't belong to any of the single languages available for that letter.
        // These characters can be removed from this list after they've been added to one of the other languages for that specific letter.
        private static string[] GetDefaultLetterKeyAllLanguagesOnly(LetterKey letter)
        {
            return letter switch
            {
                LetterKey.VK_0 => new[] { "₀", "⁰", "°", "↉" },
                LetterKey.VK_1 => new[] { "₁", "¹", "½", "⅓", "¼", "⅕", "⅙", "⅐", "⅛", "⅑", "⅒" },
                LetterKey.VK_2 => new[] { "₂", "²", "⅔", "⅖" },
                LetterKey.VK_3 => new[] { "₃", "³", "¾", "⅗", "⅜" },
                LetterKey.VK_4 => new[] { "₄", "⁴", "⅘" },
                LetterKey.VK_5 => new[] { "₅", "⁵", "⅚", "⅝" },
                LetterKey.VK_6 => new[] { "₆", "⁶" },
                LetterKey.VK_7 => new[] { "₇", "⁷", "⅞" },
                LetterKey.VK_8 => new[] { "₈", "⁸", "∞" },
                LetterKey.VK_9 => new[] { "₉", "⁹" },
                LetterKey.VK_A => new[] { "ȧ", "ǽ", "∀" },
                LetterKey.VK_B => new[] { "ḃ" },
                LetterKey.VK_C => new[] { "ċ", "°C", "©", "ℂ", "∁" },
                LetterKey.VK_D => new[] { "ḍ", "ḋ", "∂" },
                LetterKey.VK_E => new[] { "∈", "∃", "∄", "∉", "ĕ" },
                LetterKey.VK_F => new[] { "ḟ", "°F" },
                LetterKey.VK_G => new[] { "ģ", "ǧ", "ġ", "ĝ", "ǥ" },
                LetterKey.VK_H => new[] { "ḣ", "ĥ", "ħ" },
                LetterKey.VK_J => new[] { "ĵ" },
                LetterKey.VK_K => new[] { "ķ", "ǩ" },
                LetterKey.VK_L => new[] { "ļ", "₺" }, // ₺ is in VK_T for other languages, but not VK_L, so we add it here.
                LetterKey.VK_M => new[] { "ṁ" },
                LetterKey.VK_N => new[] { "ņ", "ṅ", "ⁿ", "ℕ", "№" },
                LetterKey.VK_O => new[] { "ȯ", "∅" },
                LetterKey.VK_P => new[] { "ṗ", "℗", "∏", "¶" },
                LetterKey.VK_Q => new[] { "ℚ" },
                LetterKey.VK_R => new[] { "ṙ", "®", "ℝ" },
                LetterKey.VK_S => new[] { "ṡ", "§", "∑", "∫" },
                LetterKey.VK_T => new[] { "ţ", "ṫ", "ŧ", "™" },
                LetterKey.VK_U => new[] { "ŭ" },
                LetterKey.VK_V => new[] { "V̇" },
                LetterKey.VK_W => new[] { "ẇ" },
                LetterKey.VK_X => new[] { "ẋ", "×" },
                LetterKey.VK_Y => new[] { "ẏ", "ꝡ" },
                LetterKey.VK_Z => new[] { "ʒ", "ǯ", "ℤ" },
                LetterKey.VK_COMMA => new[] { "∙", "₋", "⁻", "–", "√" }, // – is in VK_MINUS for other languages, but not VK_COMMA, so we add it here.
                LetterKey.VK_PERIOD => new[] { "…", "⁝", "\u0300", "\u0301", "\u0302", "\u0303", "\u0304", "\u0308", "\u030B", "\u030C" },
                LetterKey.VK_MINUS => new[] { "~", "‐", "‑", "‒", "—", "―", "⁓", "−", "⸺", "⸻", "∓" },
                LetterKey.VK_SLASH_ => new[] { "÷", "√" },
                LetterKey.VK_DIVIDE_ => new[] { "÷", "√", "‽" },
                LetterKey.VK_MULTIPLY_ => new[] { "×", "⋅" },
                LetterKey.VK_PLUS => new[] { "≤", "≥", "≠", "≈", "≙", "⊕", "⊗", "∓", "≅", "≡" },
                _ => Array.Empty<string>(),
            };
        }

        // Bulgarian
        private static string[] GetDefaultLetterKeyBG(LetterKey letter)
        {
            return letter switch
            {
                LetterKey.VK_I => new[] { "й" },
                _ => Array.Empty<string>(),
            };
        }

        // Crimean Tatar
        private static string[] GetDefaultLetterKeyCRH(LetterKey letter)
        {
            return letter switch
            {
                LetterKey.VK_A => new[] { "â" },
                LetterKey.VK_C => new[] { "ç" },
                LetterKey.VK_E => new[] { "€" },
                LetterKey.VK_G => new[] { "ğ" },
                LetterKey.VK_H => new[] { "₴" },
                LetterKey.VK_I => new[] { "ı", "İ" },
                LetterKey.VK_N => new[] { "ñ" },
                LetterKey.VK_O => new[] { "ö" },
                LetterKey.VK_S => new[] { "ş" },
                LetterKey.VK_T => new[] { "₺" },
                LetterKey.VK_U => new[] { "ü" },
                _ => Array.Empty<string>(),
            };
        }

        // Currencies (source: https://www.eurochange.co.uk/travel-money/world-currency-abbreviations-symbols-and-codes-travel-money)
        private static string[] GetDefaultLetterKeyCUR(LetterKey letter)
        {
            return letter switch
            {
                LetterKey.VK_B => new[] { "฿", "в" },
                LetterKey.VK_C => new[] { "¢", "₡", "č" },
                LetterKey.VK_D => new[] { "₫" },
                LetterKey.VK_E => new[] { "€" },
                LetterKey.VK_F => new[] { "ƒ" },
                LetterKey.VK_H => new[] { "₴" },
                LetterKey.VK_K => new[] { "₭" },
                LetterKey.VK_L => new[] { "ł" },
                LetterKey.VK_N => new[] { "л" },
                LetterKey.VK_M => new[] { "₼" },
                LetterKey.VK_P => new[] { "£", "₽" },
                LetterKey.VK_R => new[] { "₹", "៛", "﷼" },
                LetterKey.VK_S => new[] { "$", "₪" },
                LetterKey.VK_T => new[] { "₮", "₺", "₸" },
                LetterKey.VK_W => new[] { "₩" },
                LetterKey.VK_Y => new[] { "¥" },
                LetterKey.VK_Z => new[] { "z" },
                _ => Array.Empty<string>(),
            };
        }

        // Croatian
        private static string[] GetDefaultLetterKeyHR(LetterKey letter)
        {
            return letter switch
            {
                LetterKey.VK_C => new[] { "ć", "č" },
                LetterKey.VK_D => new[] { "đ" },
                LetterKey.VK_E => new[] { "€" },
                LetterKey.VK_S => new[] { "š" },
                LetterKey.VK_Z => new[] { "ž" },
                _ => Array.Empty<string>(),
            };
        }

        // Estonian
        private static string[] GetDefaultLetterKeyEST(LetterKey letter)
        {
            return letter switch
            {
                LetterKey.VK_A => new[] { "ä" },
                LetterKey.VK_E => new[] { "€" },
                LetterKey.VK_O => new[] { "ö", "õ" },
                LetterKey.VK_U => new[] { "ü" },
                LetterKey.VK_Z => new[] { "ž" },
                LetterKey.VK_S => new[] { "š" },
                _ => Array.Empty<string>(),
            };
        }

        // Esperanto
        private static string[] GetDefaultLetterKeyEPO(LetterKey letter)
        {
            return letter switch
            {
                LetterKey.VK_C => new[] { "ĉ" },
                LetterKey.VK_G => new[] { "ĝ" },
                LetterKey.VK_H => new[] { "ĥ" },
                LetterKey.VK_J => new[] { "ĵ" },
                LetterKey.VK_S => new[] { "ŝ" },
                LetterKey.VK_U => new[] { "ǔ" },
                _ => Array.Empty<string>(),
            };
        }

        // Finnish
        private static string[] GetDefaultLetterKeyFI(LetterKey letter)
        {
            return letter switch
            {
                LetterKey.VK_A => new[] { "ä", "å" },
                LetterKey.VK_E => new[] { "€" },
                LetterKey.VK_O => new[] { "ö" },
                _ => Array.Empty<string>(),
            };
        }

        // French
        private static string[] GetDefaultLetterKeyFR(LetterKey letter)
        {
            return letter switch
            {
                LetterKey.VK_A => new[] { "à", "â", "á", "ä", "ã", "æ" },
                LetterKey.VK_C => new[] { "ç" },
                LetterKey.VK_E => new[] { "é", "è", "ê", "ë", "€" },
                LetterKey.VK_I => new[] { "î", "ï", "í", "ì" },
                LetterKey.VK_O => new[] { "ô", "ö", "ó", "ò", "õ", "œ" },
                LetterKey.VK_U => new[] { "û", "ù", "ü", "ú" },
                LetterKey.VK_Y => new[] { "ÿ", "ý" },
                _ => Array.Empty<string>(),
            };
        }

        // Iceland
        private static string[] GetDefaultLetterKeyIS(LetterKey letter)
        {
            return letter switch
            {
                LetterKey.VK_A => new[] { "á", "æ" },
                LetterKey.VK_D => new[] { "ð" },
                LetterKey.VK_E => new[] { "é" },
                LetterKey.VK_O => new[] { "ó", "ö" },
                LetterKey.VK_U => new[] { "ú" },
                LetterKey.VK_Y => new[] { "ý" },
                LetterKey.VK_T => new[] { "þ" },
                _ => Array.Empty<string>(),
            };
        }

        // Spain
        private static string[] GetDefaultLetterKeySP(LetterKey letter)
        {
            return letter switch
            {
                LetterKey.VK_A => new[] { "á" },
                LetterKey.VK_E => new[] { "é", "€" },
                LetterKey.VK_H => new[] { "ḥ" },
                LetterKey.VK_I => new[] { "í" },
                LetterKey.VK_L => new[] { "ḷ" },
                LetterKey.VK_N => new[] { "ñ" },
                LetterKey.VK_O => new[] { "ó" },
                LetterKey.VK_U => new[] { "ú", "ü" },
                LetterKey.VK_COMMA => new[] { "¿", "?", "¡", "!" },
                _ => Array.Empty<string>(),
            };
        }

        // Catalan
        private static string[] GetDefaultLetterKeyCA(LetterKey letter)
        {
            return letter switch
            {
                LetterKey.VK_A => new[] { "à", "á" },
                LetterKey.VK_C => new[] { "ç" },
                LetterKey.VK_E => new[] { "è", "é", "€" },
                LetterKey.VK_I => new[] { "ì", "í", "ï" },
                LetterKey.VK_N => new[] { "ñ" },
                LetterKey.VK_O => new[] { "ò", "ó" },
                LetterKey.VK_U => new[] { "ù", "ú", "ü" },
                LetterKey.VK_L => new[] { "·" },
                LetterKey.VK_COMMA => new[] { "¿", "?", "¡", "!" },
                _ => Array.Empty<string>(),
            };
        }

        // Maori
        private static string[] GetDefaultLetterKeyMI(LetterKey letter)
        {
            return letter switch
            {
                LetterKey.VK_A => new[] { "ā" },
                LetterKey.VK_E => new[] { "ē" },
                LetterKey.VK_I => new[] { "ī" },
                LetterKey.VK_O => new[] { "ō" },
                LetterKey.VK_S => new[] { "$" },
                LetterKey.VK_U => new[] { "ū" },
                _ => Array.Empty<string>(),
            };
        }

        // Dutch
        private static string[] GetDefaultLetterKeyNL(LetterKey letter)
        {
            return letter switch
            {
                LetterKey.VK_A => new[] { "á", "à", "ä" },
                LetterKey.VK_C => new[] { "ç" },
                LetterKey.VK_E => new[] { "é", "è", "ë", "ê", "€" },
                LetterKey.VK_I => new[] { "í", "ï", "î" },
                LetterKey.VK_N => new[] { "ñ" },
                LetterKey.VK_O => new[] { "ó", "ö", "ô" },
                LetterKey.VK_U => new[] { "ú", "ü", "û" },
                _ => Array.Empty<string>(),
            };
        }

        // Pinyin
        private static string[] GetDefaultLetterKeyPI(LetterKey letter)
        {
            return letter switch
            {
                LetterKey.VK_1 => new[] { "\u0304", "ˉ" },
                LetterKey.VK_2 => new[] { "\u0301", "ˊ" },
                LetterKey.VK_3 => new[] { "\u030c", "ˇ" },
                LetterKey.VK_4 => new[] { "\u0300", "ˋ" },
                LetterKey.VK_5 => new[] { "·" },
                LetterKey.VK_A => new[] { "ā", "á", "ǎ", "à", "ɑ", "ɑ\u0304", "ɑ\u0301", "ɑ\u030c", "ɑ\u0300" },
                LetterKey.VK_C => new[] { "ĉ" },
                LetterKey.VK_E => new[] { "ē", "é", "ě", "è", "ê", "ê\u0304", "ế", "ê\u030c", "ề" },
                LetterKey.VK_I => new[] { "ī", "í", "ǐ", "ì" },
                LetterKey.VK_M => new[] { "m\u0304", "ḿ", "m\u030c", "m\u0300" },
                LetterKey.VK_N => new[] { "n\u0304", "ń", "ň", "ǹ", "ŋ", "ŋ\u0304", "ŋ\u0301", "ŋ\u030c", "ŋ\u0300" },
                LetterKey.VK_O => new[] { "ō", "ó", "ǒ", "ò" },
                LetterKey.VK_S => new[] { "ŝ" },
                LetterKey.VK_U => new[] { "ū", "ú", "ǔ", "ù", "ü", "ǖ", "ǘ", "ǚ", "ǜ" },
                LetterKey.VK_V => new[] { "ü", "ǖ", "ǘ", "ǚ", "ǜ" },
                LetterKey.VK_Y => new[] { "¥" },
                LetterKey.VK_Z => new[] { "ẑ" },
                _ => Array.Empty<string>(),
            };
        }

        // Turkish
        private static string[] GetDefaultLetterKeyTK(LetterKey letter)
        {
            return letter switch
            {
                LetterKey.VK_A => new[] { "â" },
                LetterKey.VK_C => new[] { "ç" },
                LetterKey.VK_E => new[] { "ë", "€" },
                LetterKey.VK_G => new[] { "ğ" },
                LetterKey.VK_I => new[] { "ı", "İ", "î", },
                LetterKey.VK_O => new[] { "ö", "ô" },
                LetterKey.VK_S => new[] { "ş" },
                LetterKey.VK_T => new[] { "₺" },
                LetterKey.VK_U => new[] { "ü", "û" },
                _ => Array.Empty<string>(),
            };
        }

        // Polish
        private static string[] GetDefaultLetterKeyPL(LetterKey letter)
        {
            return letter switch
            {
                LetterKey.VK_A => new[] { "ą" },
                LetterKey.VK_C => new[] { "ć" },
                LetterKey.VK_E => new[] { "ę", "€" },
                LetterKey.VK_L => new[] { "ł" },
                LetterKey.VK_N => new[] { "ń" },
                LetterKey.VK_O => new[] { "ó" },
                LetterKey.VK_S => new[] { "ś" },
                LetterKey.VK_Z => new[] { "ż", "ź" },
                _ => Array.Empty<string>(),
            };
        }

        // Portuguese
        private static string[] GetDefaultLetterKeyPT(LetterKey letter)
        {
            return letter switch
            {
                LetterKey.VK_A => new[] { "á", "à", "â", "ã", "ª" },
                LetterKey.VK_C => new[] { "ç" },
                LetterKey.VK_E => new[] { "é", "ê", "€" },
                LetterKey.VK_I => new[] { "í" },
                LetterKey.VK_O => new[] { "ô", "ó", "õ", "º" },
                LetterKey.VK_P => new[] { "π" },
                LetterKey.VK_S => new[] { "$" },
                LetterKey.VK_U => new[] { "ú" },
                LetterKey.VK_COMMA => new[] { "≤", "≥", "≠", "≈", "≙", "±", "₊", "⁺" },
                _ => Array.Empty<string>(),
            };
        }

        // Middle Eastern Romanization
        private static string[] GetDefaultLetterKeyROM(LetterKey letter)
        {
            return letter switch
            {
                LetterKey.VK_A => new[] { "á", "â", "ă", "ā" },
                LetterKey.VK_B => new[] { "ḇ" },
                LetterKey.VK_C => new[] { "č", "ç" },
                LetterKey.VK_D => new[] { "ḑ", "ḍ", "ḏ", "ḏ\u0323" },
                LetterKey.VK_E => new[] { "ê", "ě", "ĕ", "ē", "é", "ə" },
                LetterKey.VK_G => new[] { "ġ", "ǧ", "ğ", "ḡ", "g\u0303", "g\u0331" },
                LetterKey.VK_H => new[] { "ḧ", "ḩ", "ḥ", "ḫ", "h\u0331" },
                LetterKey.VK_I => new[] { "í", "ı", "î", "ī", "ı\u0307\u0304" },
                LetterKey.VK_J => new[] { "ǰ", "j\u0331" },
                LetterKey.VK_K => new[] { "ḳ", "ḵ" },
                LetterKey.VK_L => new[] { "ł" },
                LetterKey.VK_N => new[] { "ⁿ", "ñ" },
                LetterKey.VK_O => new[] { "ó", "ô", "ö", "ŏ", "ō", "ȫ" },
                LetterKey.VK_P => new[] { "p\u0304" },
                LetterKey.VK_R => new[] { "ṙ", "ṛ" },
                LetterKey.VK_S => new[] { "ś", "š", "ş", "ṣ", "s\u0331", "ṣ\u0304" },
                LetterKey.VK_T => new[] { "ẗ", "ţ", "ṭ", "ṯ" },
                LetterKey.VK_U => new[] { "ú", "û", "ü", "ū", "ǖ" },
                LetterKey.VK_V => new[] { "v\u0307", "ṿ", "ᵛ" },
                LetterKey.VK_Y => new[] { "̀y" },
                LetterKey.VK_Z => new[] { "ż", "ž", "z\u0304", "z\u0327", "ẓ", "z\u0324", "ẕ" },
                LetterKey.VK_PERIOD => new[] { "’", "ʾ", "ʿ", "′", "…" },
                _ => Array.Empty<string>(),
            };
        }

        // Slovak
        private static string[] GetDefaultLetterKeySK(LetterKey letter)
        {
            return letter switch
            {
                LetterKey.VK_A => new[] { "á", "ä" },
                LetterKey.VK_C => new[] { "č" },
                LetterKey.VK_D => new[] { "ď" },
                LetterKey.VK_E => new[] { "é", "€" },
                LetterKey.VK_I => new[] { "í" },
                LetterKey.VK_L => new[] { "ľ", "ĺ" },
                LetterKey.VK_N => new[] { "ň" },
                LetterKey.VK_O => new[] { "ó", "ô" },
                LetterKey.VK_R => new[] { "ŕ" },
                LetterKey.VK_S => new[] { "š" },
                LetterKey.VK_T => new[] { "ť" },
                LetterKey.VK_U => new[] { "ú" },
                LetterKey.VK_Y => new[] { "ý" },
                LetterKey.VK_Z => new[] { "ž" },
                _ => Array.Empty<string>(),
            };
        }

        // Gaeilge (Irish language)
        private static string[] GetDefaultLetterKeyGA(LetterKey letter)
        {
            return letter switch
            {
                LetterKey.VK_A => new[] { "á" },
                LetterKey.VK_E => new[] { "é", "€" },
                LetterKey.VK_I => new[] { "í" },
                LetterKey.VK_O => new[] { "ó" },
                LetterKey.VK_U => new[] { "ú" },
                _ => Array.Empty<string>(),
            };
        }

        // Gàidhlig (Scottish Gaelic)
        private static string[] GetDefaultLetterKeyGD(LetterKey letter)
        {
            return letter switch
            {
                LetterKey.VK_A => new[] { "à" },
                LetterKey.VK_E => new[] { "è" },
                LetterKey.VK_I => new[] { "ì" },
                LetterKey.VK_O => new[] { "ò" },
                LetterKey.VK_P => new[] { "£" },
                LetterKey.VK_U => new[] { "ù" },
                _ => Array.Empty<string>(),
            };
        }

        // Czech
        private static string[] GetDefaultLetterKeyCZ(LetterKey letter)
        {
            return letter switch
            {
                LetterKey.VK_A => new[] { "á" },
                LetterKey.VK_C => new[] { "č" },
                LetterKey.VK_D => new[] { "ď" },
                LetterKey.VK_E => new[] { "ě", "é" },
                LetterKey.VK_I => new[] { "í" },
                LetterKey.VK_N => new[] { "ň" },
                LetterKey.VK_O => new[] { "ó" },
                LetterKey.VK_R => new[] { "ř" },
                LetterKey.VK_S => new[] { "š" },
                LetterKey.VK_T => new[] { "ť" },
                LetterKey.VK_U => new[] { "ů", "ú" },
                LetterKey.VK_Y => new[] { "ý" },
                LetterKey.VK_Z => new[] { "ž" },
                _ => Array.Empty<string>(),
            };
        }

        // German
        private static string[] GetDefaultLetterKeyDE(LetterKey letter)
        {
            return letter switch
            {
                LetterKey.VK_A => new[] { "ä" },
                LetterKey.VK_E => new[] { "€" },
                LetterKey.VK_O => new[] { "ö" },
                LetterKey.VK_S => new[] { "ß" },
                LetterKey.VK_U => new[] { "ü" },
                _ => Array.Empty<string>(),
            };
        }

        // Greek
        private static string[] GetDefaultLetterKeyEL(LetterKey letter)
        {
            return letter switch
            {
                LetterKey.VK_A => new string[] { "α", "ά" },
                LetterKey.VK_B => new string[] { "β" },
                LetterKey.VK_C => new string[] { "χ" },
                LetterKey.VK_D => new string[] { "δ" },
                LetterKey.VK_E => new string[] { "ε", "έ", "η", "ή" },
                LetterKey.VK_F => new string[] { "φ" },
                LetterKey.VK_G => new string[] { "γ" },
                LetterKey.VK_I => new string[] { "ι", "ί" },
                LetterKey.VK_K => new string[] { "κ" },
                LetterKey.VK_L => new string[] { "λ" },
                LetterKey.VK_M => new string[] { "μ" },
                LetterKey.VK_N => new string[] { "ν" },
                LetterKey.VK_O => new string[] { "ο", "ό", "ω", "ώ" },
                LetterKey.VK_P => new string[] { "π", "φ", "ψ" },
                LetterKey.VK_R => new string[] { "ρ" },
                LetterKey.VK_S => new string[] { "σ" },
                LetterKey.VK_T => new string[] { "τ", "θ", "ϑ" },
                LetterKey.VK_U => new string[] { "υ", "ύ" },
                LetterKey.VK_X => new string[] { "ξ" },
                LetterKey.VK_Y => new string[] { "υ" },
                LetterKey.VK_Z => new string[] { "ζ" },
                _ => Array.Empty<string>(),
            };
        }

        // Hebrew
        private static string[] GetDefaultLetterKeyHE(LetterKey letter)
        {
            return letter switch
            {
                LetterKey.VK_A => new[] { "שׂ", "שׁ", "\u05b0" },
                LetterKey.VK_B => new[] { "׆" },
                LetterKey.VK_E => new[] { "\u05b8", "\u05b3", "\u05bb" },
                LetterKey.VK_G => new[] { "ױ" },
                LetterKey.VK_H => new[] { "ײ", "ײַ", "ׯ", "\u05b4" },
                LetterKey.VK_M => new[] { "\u05b5" },
                LetterKey.VK_P => new[] { "\u05b7", "\u05b2" },
                LetterKey.VK_S => new[] { "\u05bc" },
                LetterKey.VK_T => new[] { "ﭏ" },
                LetterKey.VK_U => new[] { "וֹ", "וּ", "װ", "\u05b9" },
                LetterKey.VK_X => new[] { "\u05b6", "\u05b1" },
                LetterKey.VK_Y => new[] { "ױ" },
                LetterKey.VK_COMMA => new[] { "”", "’", "״", "׳" },
                LetterKey.VK_PERIOD => new[] { "\u05ab", "\u05bd", "\u05bf" },
                LetterKey.VK_MINUS => new[] { "–", "־" },
                _ => Array.Empty<string>(),
            };
        }

        // Hungarian
        private static string[] GetDefaultLetterKeyHU(LetterKey letter)
        {
            return letter switch
            {
                LetterKey.VK_A => new[] { "á" },
                LetterKey.VK_E => new[] { "é" },
                LetterKey.VK_I => new[] { "í" },
                LetterKey.VK_O => new[] { "ó", "ő", "ö" },
                LetterKey.VK_U => new[] { "ú", "ű", "ü" },
                _ => Array.Empty<string>(),
            };
        }

        // Romanian
        private static string[] GetDefaultLetterKeyRO(LetterKey letter)
        {
            return letter switch
            {
                LetterKey.VK_A => new[] { "ă", "â" },
                LetterKey.VK_I => new[] { "î" },
                LetterKey.VK_S => new[] { "ș" },
                LetterKey.VK_T => new[] { "ț" },
                _ => Array.Empty<string>(),
            };
        }

        // Italian
        private static string[] GetDefaultLetterKeyIT(LetterKey letter)
        {
            return letter switch
            {
                LetterKey.VK_A => new[] { "à" },
                LetterKey.VK_E => new[] { "è", "é", "ə", "€" },
                LetterKey.VK_I => new[] { "ì", "í" },
                LetterKey.VK_O => new[] { "ò", "ó" },
                LetterKey.VK_U => new[] { "ù", "ú" },
                _ => Array.Empty<string>(),
            };
        }

        // Kurdish
        private static string[] GetDefaultLetterKeyKU(LetterKey letter)
        {
            return letter switch
            {
                LetterKey.VK_C => new[] { "ç" },
                LetterKey.VK_E => new[] { "ê", "€" },
                LetterKey.VK_I => new[] { "î" },
                LetterKey.VK_O => new[] { "ö", "ô" },
                LetterKey.VK_L => new[] { "ł" },
                LetterKey.VK_N => new[] { "ň" },
                LetterKey.VK_R => new[] { "ř" },
                LetterKey.VK_S => new[] { "ş" },
                LetterKey.VK_U => new[] { "û", "ü" },
                _ => Array.Empty<string>(),
            };
        }

        // Welsh
        private static string[] GetDefaultLetterKeyCY(LetterKey letter)
        {
            return letter switch
            {
                LetterKey.VK_A => new[] { "â" },
                LetterKey.VK_E => new[] { "ê" },
                LetterKey.VK_I => new[] { "î" },
                LetterKey.VK_O => new[] { "ô" },
                LetterKey.VK_P => new[] { "£" },
                LetterKey.VK_U => new[] { "û" },
                LetterKey.VK_Y => new[] { "ŷ" },
                LetterKey.VK_W => new[] { "ŵ" },
                _ => Array.Empty<string>(),
            };
        }

        // Swedish
        private static string[] GetDefaultLetterKeySV(LetterKey letter)
        {
            return letter switch
            {
                LetterKey.VK_A => new[] { "å", "ä" },
                LetterKey.VK_E => new[] { "é" },
                LetterKey.VK_O => new[] { "ö" },
                _ => Array.Empty<string>(),
            };
        }

        // Serbian
        private static string[] GetDefaultLetterKeySR(LetterKey letter)
        {
            return letter switch
            {
                LetterKey.VK_C => new[] { "ć", "č" },
                LetterKey.VK_D => new[] { "đ" },
                LetterKey.VK_S => new[] { "š" },
                LetterKey.VK_Z => new[] { "ž" },
                _ => Array.Empty<string>(),
            };
        }

        // Macedonian
        private static string[] GetDefaultLetterKeyMK(LetterKey letter)
        {
            return letter switch
            {
                LetterKey.VK_E => new[] { "ѐ" },
                LetterKey.VK_I => new[] { "ѝ" },
                _ => Array.Empty<string>(),
            };
        }

        // Norwegian
        private static string[] GetDefaultLetterKeyNO(LetterKey letter)
        {
            return letter switch
            {
                LetterKey.VK_A => new[] { "å", "æ" },
                LetterKey.VK_E => new[] { "€", "é" },
                LetterKey.VK_O => new[] { "ø" },
                LetterKey.VK_S => new[] { "$" },
                _ => Array.Empty<string>(),
            };
        }

        // Danish
        private static string[] GetDefaultLetterKeyDK(LetterKey letter)
        {
            return letter switch
            {
                LetterKey.VK_A => new[] { "å", "æ" },
                LetterKey.VK_E => new[] { "€" },
                LetterKey.VK_O => new[] { "ø" },
                _ => Array.Empty<string>(),
            };
        }

        // Lithuanian
        private static string[] GetDefaultLetterKeyLT(LetterKey letter)
        {
            return letter switch
            {
                LetterKey.VK_A => new[] { "ą" },
                LetterKey.VK_C => new[] { "č" },
                LetterKey.VK_E => new[] { "ę", "ė", "€" },
                LetterKey.VK_I => new[] { "į" },
                LetterKey.VK_S => new[] { "š" },
                LetterKey.VK_U => new[] { "ų", "ū" },
                LetterKey.VK_Z => new[] { "ž" },
                _ => Array.Empty<string>(),
            };
        }

        // Slovenian
        private static string[] GetDefaultLetterKeySL(LetterKey letter)
        {
            return letter switch
            {
                LetterKey.VK_C => new[] { "č" },
                LetterKey.VK_E => new[] { "€" },
                LetterKey.VK_S => new[] { "š" },
                LetterKey.VK_Z => new[] { "ž" },
                _ => Array.Empty<string>(),
            };
        }

        // IPA (International Phonetic Alphabet)
        private static string[] GetDefaultLetterKeyIPA(LetterKey letter)
        {
            return letter switch
            {
                LetterKey.VK_A => new[] { "ɐ", "ɑ", "ɒ" },
                LetterKey.VK_B => new[] { "ʙ" },
                LetterKey.VK_E => new[] { "ɘ", "ɵ", "ə", "ɛ", "ɜ", "ɞ" },
                LetterKey.VK_F => new[] { "ɟ", "ɸ" },
                LetterKey.VK_G => new[] { "ɢ", "ɣ" },
                LetterKey.VK_H => new[] { "ɦ", "ʜ" },
                LetterKey.VK_I => new[] { "ɨ", "ɪ" },
                LetterKey.VK_J => new[] { "ʝ" },
                LetterKey.VK_L => new[] { "ɬ", "ɮ", "ꞎ", "ɭ", "ʎ", "ʟ", "ɺ" },
                LetterKey.VK_N => new[] { "ɳ", "ɲ", "ŋ", "ɴ" },
                LetterKey.VK_O => new[] { "ɤ", "ɔ", "ɶ" },
                LetterKey.VK_R => new[] { "ʁ", "ɹ", "ɻ", "ɾ", "ɽ", "ʀ" },
                LetterKey.VK_S => new[] { "ʃ", "ʂ", "ɕ" },
                LetterKey.VK_U => new[] { "ʉ", "ʊ" },
                LetterKey.VK_V => new[] { "ʋ", "ⱱ", "ʌ" },
                LetterKey.VK_W => new[] { "ɰ", "ɯ" },
                LetterKey.VK_Y => new[] { "ʏ" },
                LetterKey.VK_Z => new[] { "ʒ", "ʐ", "ʑ" },
                LetterKey.VK_COMMA => new[] { "ʡ", "ʔ", "ʕ", "ʢ" },
                _ => Array.Empty<string>(),
            };
        }
    }
}
