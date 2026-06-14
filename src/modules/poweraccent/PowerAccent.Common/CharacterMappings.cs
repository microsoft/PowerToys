// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Concurrent;

namespace PowerAccent.Common;

/// <summary>
/// Single source of truth for all Quick Accent character data.
/// <para>
/// <see cref="All"/> is the canonical registry of every language: its identity, group,
/// resource identifier, and character mappings. The Settings UI derives its language
/// list from this collection.
/// </para>
/// <para>
/// <see cref="DisplayOrder"/> and <see cref="GroupDisplayOrder"/> control the order
/// in which characters appear in the Quick Accent popup. These are intentionally
/// separate from <see cref="All"/> so that popup ordering is explicit and not an
/// accidental consequence of declaration order.
/// </para>
/// <para>
/// When adding a new language: add a <see cref="Language"/> enum value, a
/// <see cref="LanguageInfo"/> entry to <see cref="All"/>, a position in
/// <see cref="DisplayOrder"/>, and a resx string.
/// </para>
/// </summary>
public static class CharacterMappings
{
    /// <summary>
    /// The canonical registry of all languages. Each entry defines the language's
    /// identity, group, resource identifier, and character mappings.
    /// Declaration order here does not affect the popup or settings display order;
    /// see <see cref="DisplayOrder"/> and <see cref="GroupDisplayOrder"/> for that.
    /// </summary>
    public static readonly IReadOnlyList<LanguageInfo> All =
    [
        new(Language.SPECIAL, "Special", LanguageGroup.Special,  new Dictionary<LetterKey, string[]>
        {
            [LetterKey.VK_0] = ["₀", "⁰", "°", "↉", "₎", "⁾"],
            [LetterKey.VK_1] = ["₁", "¹", "½", "⅓", "¼", "⅕", "⅙", "⅐", "⅛", "⅑", "⅒"],
            [LetterKey.VK_2] = ["₂", "²", "⅔", "⅖"],
            [LetterKey.VK_3] = ["₃", "³", "¾", "⅗", "⅜"],
            [LetterKey.VK_4] = ["₄", "⁴", "⅘"],
            [LetterKey.VK_5] = ["₅", "⁵", "⅚", "⅝"],
            [LetterKey.VK_6] = ["₆", "⁶"],
            [LetterKey.VK_7] = ["₇", "⁷", "⅞"],
            [LetterKey.VK_8] = ["₈", "⁸", "∞"],
            [LetterKey.VK_9] = ["₉", "⁹", "₍", "⁽"],
            [LetterKey.VK_A] = ["ȧ", "ǽ", "∀", "ᵃ", "ₐ"],
            [LetterKey.VK_B] = ["ḃ", "ᵇ"],
            [LetterKey.VK_C] = ["ċ", "°C", "©", "ℂ", "∁", "ᶜ"],
            [LetterKey.VK_D] = ["ḍ", "ḋ", "∂", "ᵈ"],
            [LetterKey.VK_E] = ["∈", "∃", "∄", "∉", "ĕ", "ᵉ", "ₑ"],
            [LetterKey.VK_F] = ["ḟ", "°F", "ᶠ"],
            [LetterKey.VK_G] = ["ģ", "ǧ", "ġ", "ĝ", "ǥ", "ᵍ"],
            [LetterKey.VK_H] = ["ḣ", "ĥ", "ħ", "ʰ", "ₕ"],
            [LetterKey.VK_I] = ["ⁱ", "ᵢ"],
            [LetterKey.VK_J] = ["ĵ", "ʲ", "ⱼ"],
            [LetterKey.VK_K] = ["ķ", "ǩ", "ᵏ", "ₖ"],
            [LetterKey.VK_L] = ["ļ", "₺", "ˡ", "ₗ"], // ₺ is in VK_T for other languages, but not VK_L, so we add it here.
            [LetterKey.VK_M] = ["ṁ", "ᵐ", "ₘ"],
            [LetterKey.VK_N] = ["ņ", "ṅ", "ⁿ", "ℕ", "№", "ₙ"],
            [LetterKey.VK_O] = ["ȯ", "∅", "⌀", "ᵒ", "ₒ"],
            [LetterKey.VK_P] = ["ṗ", "℗", "∏", "¶", "ᵖ", "ₚ"],
            [LetterKey.VK_Q] = ["ℚ", "𐞥"],
            [LetterKey.VK_R] = ["ṙ", "®", "ℝ", "ʳ", "ᵣ"],
            [LetterKey.VK_S] = ["ṡ", "§", "∑", "∫", "ˢ", "ₛ"],
            [LetterKey.VK_T] = ["ţ", "ṫ", "ŧ", "™", "ᵗ", "ₜ"],
            [LetterKey.VK_U] = ["ŭ", "ᵘ", "ᵤ"],
            [LetterKey.VK_V] = ["V̇", "ᵛ", "ᵥ"],
            [LetterKey.VK_W] = ["ẇ", "ʷ"],
            [LetterKey.VK_X] = ["ẋ", "×", "ˣ", "ₓ"],
            [LetterKey.VK_Y] = ["ẏ", "ꝡ", "ʸ"],
            [LetterKey.VK_Z] = ["ʒ", "ǯ", "ℤ", "ᶻ"],
            [LetterKey.VK_COMMA] = ["∙", "₋", "⁻", "–", "√", "‟", "《", "》", "‛", "〈", "〉", "″", "‴", "⁗"], // – is in VK_MINUS for other languages, but not VK_COMMA, so we add it here.
            [LetterKey.VK_PERIOD] = ["…", "⁝", "\u0300", "\u0301", "\u0302", "\u0303", "\u0304", "\u0308", "\u030B", "\u030C"],
            [LetterKey.VK_MINUS] = ["~", "‐", "‑", "‒", "–", "—", "―", "⁓", "−", "⸺", "⸻", "∓", "₋", "⁻"],
            [LetterKey.VK_SLASH_] = ["÷", "√"],
            [LetterKey.VK_DIVIDE_] = ["÷", "√"],
            [LetterKey.VK_MULTIPLY_] = ["×", "⋅", "ˣ", "ₓ"],
            [LetterKey.VK_PLUS] = ["≤", "≥", "≠", "≈", "≙", "⊕", "⊗", "±", "≅", "≡", "₊", "⁺", "₌", "⁼"],
            [LetterKey.VK_BACKSLASH] = ["`", "~"],
        }),

        new(Language.BG, "Bulgarian", LanguageGroup.Language, new Dictionary<LetterKey, string[]>
        {
            [LetterKey.VK_I] = ["й"],
        }),

        new(Language.CA, "Catalan", LanguageGroup.Language, new Dictionary<LetterKey, string[]>
        {
            [LetterKey.VK_A] = ["à", "á"],
            [LetterKey.VK_C] = ["ç"],
            [LetterKey.VK_E] = ["è", "é", "€"],
            [LetterKey.VK_I] = ["ì", "í", "ï"],
            [LetterKey.VK_N] = ["ñ"],
            [LetterKey.VK_O] = ["ò", "ó"],
            [LetterKey.VK_U] = ["ù", "ú", "ü"],
            [LetterKey.VK_L] = ["·"],
            [LetterKey.VK_COMMA] = ["¿", "?", "¡", "!", "«", "»", "“", "”", "‘", "’"],
        }),

        new(Language.CRH, "Crimean", LanguageGroup.Language, new Dictionary<LetterKey, string[]>
        {
            [LetterKey.VK_A] = ["â"],
            [LetterKey.VK_C] = ["ç"],
            [LetterKey.VK_E] = ["€"],
            [LetterKey.VK_G] = ["ğ"],
            [LetterKey.VK_H] = ["₴"],
            [LetterKey.VK_I] = ["ı", "İ"],
            [LetterKey.VK_N] = ["ñ"],
            [LetterKey.VK_O] = ["ö"],
            [LetterKey.VK_S] = ["ş"],
            [LetterKey.VK_T] = ["₺"],
            [LetterKey.VK_U] = ["ü"],
        }),

        // Currency symbols. This is a "special" language group as it's not a spoken
        // language, but rather a set of symbols used across languages.
        new(Language.CUR, "Currency", LanguageGroup.Special, new Dictionary<LetterKey, string[]>
        {
            [LetterKey.VK_B] = ["฿", "в"],
            [LetterKey.VK_C] = ["¢", "₡", "č"],
            [LetterKey.VK_D] = ["₫"],
            [LetterKey.VK_E] = ["€"],
            [LetterKey.VK_F] = ["ƒ"],
            [LetterKey.VK_H] = ["₴"],
            [LetterKey.VK_K] = ["₭"],
            [LetterKey.VK_L] = ["ł"],
            [LetterKey.VK_N] = ["л"],
            [LetterKey.VK_M] = ["₼"],
            [LetterKey.VK_P] = ["£", "₽"],
            [LetterKey.VK_R] = ["₹", "៛", "﷼"],
            [LetterKey.VK_S] = ["$", "₪"],
            [LetterKey.VK_T] = ["₮", "₺", "₸"],
            [LetterKey.VK_W] = ["₩"],
            [LetterKey.VK_Y] = ["¥"],
            [LetterKey.VK_Z] = ["z"],
        }),

        new(Language.CY, "Welsh", LanguageGroup.Language, new Dictionary<LetterKey, string[]>
        {
            [LetterKey.VK_A] = ["â", "ä", "à", "á"],
            [LetterKey.VK_E] = ["ê", "ë", "è", "é"],
            [LetterKey.VK_I] = ["î", "ï", "ì", "í"],
            [LetterKey.VK_O] = ["ô", "ö", "ò", "ó"],
            [LetterKey.VK_P] = ["£"],
            [LetterKey.VK_U] = ["û", "ü", "ù", "ú"],
            [LetterKey.VK_Y] = ["ŷ", "ÿ", "ỳ", "ý"],
            [LetterKey.VK_W] = ["ŵ", "ẅ", "ẁ", "ẃ"],
            [LetterKey.VK_COMMA] = ["‘", "’", "“", "”"],
        }),

        new(Language.CZ, "Czech", LanguageGroup.Language, new Dictionary<LetterKey, string[]>
        {
            [LetterKey.VK_A] = ["á"],
            [LetterKey.VK_C] = ["č"],
            [LetterKey.VK_D] = ["ď"],
            [LetterKey.VK_E] = ["ě", "é"],
            [LetterKey.VK_I] = ["í"],
            [LetterKey.VK_N] = ["ň"],
            [LetterKey.VK_O] = ["ó"],
            [LetterKey.VK_R] = ["ř"],
            [LetterKey.VK_S] = ["š"],
            [LetterKey.VK_T] = ["ť"],
            [LetterKey.VK_U] = ["ů", "ú"],
            [LetterKey.VK_Y] = ["ý"],
            [LetterKey.VK_Z] = ["ž"],
            [LetterKey.VK_COMMA] = ["„", "“", "‚", "‘", "»", "«", "›", "‹"],
        }),

        new(Language.DK, "Danish", LanguageGroup.Language, new Dictionary<LetterKey, string[]>
        {
            [LetterKey.VK_A] = ["å", "æ"],
            [LetterKey.VK_E] = ["€"],
            [LetterKey.VK_O] = ["ø"],
            [LetterKey.VK_COMMA] = ["»", "«", "“", "”", "›", "‹", "‘", "’"],
        }),

        // Gaelic (Irish).
        new(Language.GA, "Gaeilge", LanguageGroup.Language, new Dictionary<LetterKey, string[]>
        {
            [LetterKey.VK_A] = ["á"],
            [LetterKey.VK_E] = ["é", "€"],
            [LetterKey.VK_I] = ["í"],
            [LetterKey.VK_O] = ["ó"],
            [LetterKey.VK_U] = ["ú"],
            [LetterKey.VK_COMMA] = ["“", "”", "‘", "’"],
        }),

        // Gaelic (Scottish).
        new(Language.GD, "Gaidhlig", LanguageGroup.Language, new Dictionary<LetterKey, string[]>
        {
            [LetterKey.VK_A] = ["à"],
            [LetterKey.VK_E] = ["è"],
            [LetterKey.VK_I] = ["ì"],
            [LetterKey.VK_O] = ["ò"],
            [LetterKey.VK_P] = ["£"],
            [LetterKey.VK_U] = ["ù"],
            [LetterKey.VK_COMMA] = ["“", "”", "‘", "’"],
        }),

        new(Language.DE, "German", LanguageGroup.Language, new Dictionary<LetterKey, string[]>
        {
            [LetterKey.VK_A] = ["ä"],
            [LetterKey.VK_E] = ["€"],
            [LetterKey.VK_O] = ["ö"],
            [LetterKey.VK_S] = ["ß"],
            [LetterKey.VK_U] = ["ü"],
            [LetterKey.VK_COMMA] = ["„", "“", "‚", "‘", "»", "«", "›", "‹"],
        }),

        new(Language.EL, "Greek", LanguageGroup.Language, new Dictionary<LetterKey, string[]>
        {
            [LetterKey.VK_A] = ["α", "ά"],
            [LetterKey.VK_B] = ["β"],
            [LetterKey.VK_C] = ["χ"],
            [LetterKey.VK_D] = ["δ"],
            [LetterKey.VK_E] = ["ε", "έ", "η", "ή"],
            [LetterKey.VK_F] = ["φ"],
            [LetterKey.VK_G] = ["γ"],
            [LetterKey.VK_I] = ["ι", "ί"],
            [LetterKey.VK_K] = ["κ"],
            [LetterKey.VK_L] = ["λ"],
            [LetterKey.VK_M] = ["μ"],
            [LetterKey.VK_N] = ["ν"],
            [LetterKey.VK_O] = ["ο", "ό", "ω", "ώ"],
            [LetterKey.VK_P] = ["π", "φ", "ψ"],
            [LetterKey.VK_R] = ["ρ"],
            [LetterKey.VK_S] = ["σ", "ς"],
            [LetterKey.VK_T] = ["τ", "θ", "ϑ"],
            [LetterKey.VK_U] = ["υ", "ύ"],
            [LetterKey.VK_X] = ["ξ"],
            [LetterKey.VK_Y] = ["υ"],
            [LetterKey.VK_Z] = ["ζ"],
            [LetterKey.VK_COMMA] = ["“", "”", "«", "»"],
        }),

        new(Language.EST, "Estonian", LanguageGroup.Language, new Dictionary<LetterKey, string[]>
        {
            [LetterKey.VK_A] = ["ä"],
            [LetterKey.VK_E] = ["€"],
            [LetterKey.VK_O] = ["ö", "õ"],
            [LetterKey.VK_U] = ["ü"],
            [LetterKey.VK_Z] = ["ž"],
            [LetterKey.VK_S] = ["š"],
            [LetterKey.VK_COMMA] = ["„", "“", "«", "»"],
        }),

        new(Language.EPO, "Esperanto", LanguageGroup.Language, new Dictionary<LetterKey, string[]>
        {
            [LetterKey.VK_C] = ["ĉ"],
            [LetterKey.VK_G] = ["ĝ"],
            [LetterKey.VK_H] = ["ĥ"],
            [LetterKey.VK_J] = ["ĵ"],
            [LetterKey.VK_S] = ["ŝ"],
            [LetterKey.VK_U] = ["ŭ"],
        }),

        new(Language.FI, "Finnish", LanguageGroup.Language, new Dictionary<LetterKey, string[]>
        {
            [LetterKey.VK_A] = ["ä", "å"],
            [LetterKey.VK_E] = ["€"],
            [LetterKey.VK_O] = ["ö"],
            [LetterKey.VK_COMMA] = ["”", "’", "»"],
        }),

        new(Language.FR, "French", LanguageGroup.Language, new Dictionary<LetterKey, string[]>
        {
            [LetterKey.VK_A] = ["à", "â", "á", "ä", "ã", "æ"],
            [LetterKey.VK_C] = ["ç"],
            [LetterKey.VK_E] = ["é", "è", "ê", "ë", "€"],
            [LetterKey.VK_I] = ["î", "ï", "í", "ì"],
            [LetterKey.VK_O] = ["ô", "ö", "ó", "ò", "õ", "œ"],
            [LetterKey.VK_U] = ["û", "ù", "ü", "ú"],
            [LetterKey.VK_Y] = ["ÿ", "ý"],
            [LetterKey.VK_COMMA] = ["«", "»", "‹", "›", "“", "”", "‘", "’"],
        }),

        new(Language.GRC, "Greek_Polytonic", LanguageGroup.Special, new Dictionary<LetterKey, string[]>
        {
            [LetterKey.VK_A] = ["α", "ἀ", "ἁ", "ὰ", "ά", "ᾶ", "ᾱ", "ᾰ", "ἂ", "ἃ", "ἄ", "ἅ", "ἆ", "ἇ", "ᾳ", "ᾀ", "ᾁ", "ᾴ", "ᾲ", "ᾷ", "ᾄ", "ᾅ", "ᾂ", "ᾃ", "ᾆ", "ᾇ"],
            [LetterKey.VK_B] = ["β"],
            [LetterKey.VK_C] = ["χ", "ϲ"],
            [LetterKey.VK_D] = ["δ"],
            [LetterKey.VK_E] = ["ε", "ἐ", "ἑ", "ὲ", "έ", "ἒ", "ἓ", "ἔ", "ἕ"],
            [LetterKey.VK_F] = ["φ", "ϝ"],
            [LetterKey.VK_G] = ["γ"],
            [LetterKey.VK_H] = ["η", "ἠ", "ἡ", "ὴ", "ή", "ῆ", "ἢ", "ἣ", "ἤ", "ἥ", "ἦ", "ἧ", "ῃ", "ᾐ", "ᾑ", "ῄ", "ῂ", "ῇ", "ᾔ", "ᾕ", "ᾒ", "ᾓ", "ᾖ", "ᾗ"],
            [LetterKey.VK_I] = ["ι", "ἰ", "ἱ", "ὶ", "ί", "ῖ", "ῑ", "ῐ", "ἲ", "ἳ", "ἴ", "ἵ", "ἶ", "ἷ", "ϊ", "ΐ", "ῒ", "ῗ"],
            [LetterKey.VK_K] = ["κ"],
            [LetterKey.VK_L] = ["λ"],
            [LetterKey.VK_M] = ["μ"],
            [LetterKey.VK_N] = ["ν"],
            [LetterKey.VK_O] = ["ο", "ὀ", "ὁ", "ὸ", "ό", "ὂ", "ὃ", "ὄ", "ὅ"],
            [LetterKey.VK_P] = ["π", "φ", "ψ", "ρ"],
            [LetterKey.VK_Q] = ["ϙ", "ϟ"],
            [LetterKey.VK_R] = ["ρ", "ῤ", "ῥ"],
            [LetterKey.VK_S] = ["σ", "ς", "ϛ", "ϲ", "ϡ"],
            [LetterKey.VK_T] = ["τ", "θ", "ϑ"],
            [LetterKey.VK_U] = ["υ", "ὐ", "ὑ", "ὺ", "ύ", "ῦ", "ῡ", "ῠ", "ὒ", "ὓ", "ὔ", "ὕ", "ὖ", "ὗ", "ϋ", "ΰ", "ῢ", "ῧ"],
            [LetterKey.VK_V] = ["β", "ϝ"],
            [LetterKey.VK_W] = ["ω", "ὠ", "ὡ", "ὼ", "ώ", "ῶ", "ὢ", "ὣ", "ὤ", "ὥ", "ὦ", "ὧ", "ῳ", "ᾠ", "ᾡ", "ῴ", "ῲ", "ῷ", "ᾤ", "ᾥ", "ᾢ", "ᾣ", "ᾦ", "ᾧ"],
            [LetterKey.VK_X] = ["ξ", "χ"],
            [LetterKey.VK_Y] = ["υ", "ὐ", "ὑ", "ὺ", "ύ", "ῦ", "ῡ", "ῠ", "ὒ", "ὓ", "ὔ", "ὕ", "ὖ", "ὗ", "ϋ", "ΰ", "ῢ", "ῧ"],
            [LetterKey.VK_Z] = ["ζ"],
            [LetterKey.VK_COMMA] = ["“", "”", "‘", "’", ";", "`", "´"],
            [LetterKey.VK_PERIOD] = ["·"],
        }),

        new(Language.HR, "Croatian", LanguageGroup.Language, new Dictionary<LetterKey, string[]>
        {
            [LetterKey.VK_C] = ["ć", "č"],
            [LetterKey.VK_D] = ["đ"],
            [LetterKey.VK_E] = ["€"],
            [LetterKey.VK_S] = ["š"],
            [LetterKey.VK_Z] = ["ž"],
            [LetterKey.VK_COMMA] = ["„", "“", "»", "«"],
        }),

        new(Language.HE, "Hebrew", LanguageGroup.Language, new Dictionary<LetterKey, string[]>
        {
            [LetterKey.VK_A] = ["שׂ", "שׁ", "\u05b0"],
            [LetterKey.VK_B] = ["׆"],
            [LetterKey.VK_E] = ["\u05b8", "\u05b3", "\u05bb"],
            [LetterKey.VK_G] = ["ױ"],
            [LetterKey.VK_H] = ["ײ", "ײַ", "ׯ", "\u05b4"],
            [LetterKey.VK_M] = ["\u05b5"],
            [LetterKey.VK_P] = ["\u05b7", "\u05b2"],
            [LetterKey.VK_S] = ["\u05bc"],
            [LetterKey.VK_T] = ["ﭏ"],
            [LetterKey.VK_U] = ["וֹ", "וּ", "װ", "\u05b9"],
            [LetterKey.VK_X] = ["\u05b6", "\u05b1"],
            [LetterKey.VK_Y] = ["ױ"],
            [LetterKey.VK_COMMA] = ["”", "’", "'", "״", "׳"],
            [LetterKey.VK_PERIOD] = ["\u05ab", "\u05bd", "\u05bf"],
            [LetterKey.VK_MINUS] = ["־"],
        }),

        new(Language.HU, "Hungarian", LanguageGroup.Language, new Dictionary<LetterKey, string[]>
        {
            [LetterKey.VK_A] = ["á"],
            [LetterKey.VK_E] = ["é"],
            [LetterKey.VK_I] = ["í"],
            [LetterKey.VK_O] = ["ó", "ő", "ö"],
            [LetterKey.VK_U] = ["ú", "ű", "ü"],
            [LetterKey.VK_Y] = ["ÿ", "ý"],
            [LetterKey.VK_COMMA] = ["„", "”", "»", "«"],
        }),

        new(Language.IS, "Icelandic", LanguageGroup.Language, new Dictionary<LetterKey, string[]>
        {
            [LetterKey.VK_A] = ["á", "æ"],
            [LetterKey.VK_D] = ["ð"],
            [LetterKey.VK_E] = ["é"],
            [LetterKey.VK_I] = ["í"],
            [LetterKey.VK_O] = ["ó", "ö"],
            [LetterKey.VK_U] = ["ú"],
            [LetterKey.VK_Y] = ["ý"],
            [LetterKey.VK_T] = ["þ"],
            [LetterKey.VK_COMMA] = ["„", "“", "‚", "‘"],
        }),

        // International Phonetic Alphabet. This is a "special" language group as it's not
        // a spoken language, but rather a set of symbols used across languages.
        new(Language.IPA, "IPA", LanguageGroup.Special,  new Dictionary<LetterKey, string[]>
        {
            [LetterKey.VK_A] = ["ɐ", "ɑ", "ɒ", "ǎ"],
            [LetterKey.VK_B] = ["ʙ"],
            [LetterKey.VK_E] = ["ɘ", "ɵ", "ə", "ɛ", "ɜ", "ɞ"],
            [LetterKey.VK_F] = ["ɟ", "ɸ"],
            [LetterKey.VK_G] = ["ɢ", "ɣ"],
            [LetterKey.VK_H] = ["ɦ", "ʜ"],
            [LetterKey.VK_I] = ["ɨ", "ɪ"],
            [LetterKey.VK_J] = ["ʝ"],
            [LetterKey.VK_L] = ["ɬ", "ɮ", "ꞎ", "ɭ", "ʎ", "ʟ", "ɺ"],
            [LetterKey.VK_N] = ["ɳ", "ɲ", "ŋ", "ɴ"],
            [LetterKey.VK_O] = ["ɤ", "ɔ", "ɶ", "ǒ"],
            [LetterKey.VK_R] = ["ʁ", "ɹ", "ɻ", "ɾ", "ɽ", "ʀ"],
            [LetterKey.VK_S] = ["ʃ", "ʂ", "ɕ"],
            [LetterKey.VK_U] = ["ʉ", "ʊ", "ǔ"],
            [LetterKey.VK_V] = ["ʋ", "ⱱ", "ʌ"],
            [LetterKey.VK_W] = ["ɰ", "ɯ"],
            [LetterKey.VK_Y] = ["ʏ"],
            [LetterKey.VK_Z] = ["ʒ", "ʐ", "ʑ"],
            [LetterKey.VK_COMMA] = ["ʡ", "ʔ", "ʕ", "ʢ"],
        }),

        new(Language.IT, "Italian", LanguageGroup.Language, new Dictionary<LetterKey, string[]>
        {
            [LetterKey.VK_A] = ["à"],
            [LetterKey.VK_E] = ["è", "é", "ə", "€"],
            [LetterKey.VK_I] = ["ì", "í"],
            [LetterKey.VK_O] = ["ò", "ó"],
            [LetterKey.VK_U] = ["ù", "ú"],
            [LetterKey.VK_COMMA] = ["«", "»", "“", "”", "‘", "’"],
        }),

        new(Language.KU, "Kurdish", LanguageGroup.Language, new Dictionary<LetterKey, string[]>
        {
            [LetterKey.VK_C] = ["ç"],
            [LetterKey.VK_E] = ["ê", "€"],
            [LetterKey.VK_I] = ["î"],
            [LetterKey.VK_O] = ["ö", "ô"],
            [LetterKey.VK_L] = ["ł"],
            [LetterKey.VK_N] = ["ň"],
            [LetterKey.VK_R] = ["ř"],
            [LetterKey.VK_S] = ["ş"],
            [LetterKey.VK_U] = ["û", "ü"],
            [LetterKey.VK_COMMA] = ["«", "»", "“", "”"],
        }),

        new(Language.LT, "Lithuanian", LanguageGroup.Language, new Dictionary<LetterKey, string[]>
        {
            [LetterKey.VK_A] = ["ą"],
            [LetterKey.VK_C] = ["č"],
            [LetterKey.VK_E] = ["ę", "ė", "€"],
            [LetterKey.VK_I] = ["į"],
            [LetterKey.VK_S] = ["š"],
            [LetterKey.VK_U] = ["ų", "ū"],
            [LetterKey.VK_Z] = ["ž"],
            [LetterKey.VK_COMMA] = ["„", "“", "‚", "‘"],
        }),

        new(Language.MK, "Macedonian", LanguageGroup.Language, new Dictionary<LetterKey, string[]>
        {
            [LetterKey.VK_E] = ["ѐ"],
            [LetterKey.VK_I] = ["ѝ"],
            [LetterKey.VK_COMMA] = ["„", "“", "’", "‘"],
        }),

        new(Language.MT, "Maltese", LanguageGroup.Language, new Dictionary<LetterKey, string[]>
        {
            [LetterKey.VK_A] = ["à"],
            [LetterKey.VK_C] = ["ċ"],
            [LetterKey.VK_E] = ["è", "€"],
            [LetterKey.VK_G] = ["ġ"],
            [LetterKey.VK_H] = ["ħ"],
            [LetterKey.VK_I] = ["ì"],
            [LetterKey.VK_O] = ["ò"],
            [LetterKey.VK_U] = ["ù"],
            [LetterKey.VK_Z] = ["ż"],
        }),

        new(Language.MI, "Maori", LanguageGroup.Language, new Dictionary<LetterKey, string[]>
        {
            [LetterKey.VK_A] = ["ā"],
            [LetterKey.VK_E] = ["ē"],
            [LetterKey.VK_I] = ["ī"],
            [LetterKey.VK_O] = ["ō"],
            [LetterKey.VK_S] = ["$"],
            [LetterKey.VK_U] = ["ū"],
            [LetterKey.VK_COMMA] = ["“", "”", "‘", "’"],
        }),

        new(Language.NL, "Dutch", LanguageGroup.Language, new Dictionary<LetterKey, string[]>
        {
            [LetterKey.VK_A] = ["á", "à", "ä"],
            [LetterKey.VK_C] = ["ç"],
            [LetterKey.VK_E] = ["é", "è", "ë", "ê", "€"],
            [LetterKey.VK_I] = ["í", "ï", "î"],
            [LetterKey.VK_N] = ["ñ"],
            [LetterKey.VK_O] = ["ó", "ö", "ô"],
            [LetterKey.VK_U] = ["ú", "ü", "û"],
            [LetterKey.VK_COMMA] = ["“", "„", "”", "‘", ",", "’"],
        }),

        new(Language.NO, "Norwegian", LanguageGroup.Language, new Dictionary<LetterKey, string[]>
        {
            [LetterKey.VK_A] = ["å", "æ"],
            [LetterKey.VK_E] = ["€", "é"],
            [LetterKey.VK_O] = ["ø"],
            [LetterKey.VK_S] = ["$"],
            [LetterKey.VK_COMMA] = ["«", "»", ",", "‘", "’", "„", "“"],
        }),

        new(Language.PI, "Pinyin", LanguageGroup.Language, new Dictionary<LetterKey, string[]>
        {
            [LetterKey.VK_1] = ["\u0304", "ˉ"],
            [LetterKey.VK_2] = ["\u0301", "ˊ"],
            [LetterKey.VK_3] = ["\u030c", "ˇ"],
            [LetterKey.VK_4] = ["\u0300", "ˋ"],
            [LetterKey.VK_5] = ["·"],
            [LetterKey.VK_A] = ["ā", "á", "ǎ", "à", "ɑ", "ɑ\u0304", "ɑ\u0301", "ɑ\u030c", "ɑ\u0300"],
            [LetterKey.VK_C] = ["ĉ"],
            [LetterKey.VK_E] = ["ē", "é", "ě", "è", "ê", "ê\u0304", "ế", "ê\u030c", "ề"],
            [LetterKey.VK_I] = ["ī", "í", "ǐ", "ì"],
            [LetterKey.VK_M] = ["m\u0304", "ḿ", "m\u030c", "m\u0300"],
            [LetterKey.VK_N] = ["n\u0304", "ń", "ň", "ǹ", "ŋ", "ŋ\u0304", "ŋ\u0301", "ŋ\u030c", "ŋ\u0300"],
            [LetterKey.VK_O] = ["ō", "ó", "ǒ", "ò"],
            [LetterKey.VK_S] = ["ŝ"],
            [LetterKey.VK_U] = ["ū", "ú", "ǔ", "ù", "ü", "ǖ", "ǘ", "ǚ", "ǜ"],
            [LetterKey.VK_V] = ["ü", "ǖ", "ǘ", "ǚ", "ǜ"],
            [LetterKey.VK_Y] = ["¥"],
            [LetterKey.VK_Z] = ["ẑ"],
            [LetterKey.VK_COMMA] = ["“", "”", "‘", "’", "「", "」", "『", "』"],
        }),

        // Proto-Indo-European. This is a "special" language group as it's not a spoken
        // language, but rather a reconstructed ancestor of many languages.
        new(Language.PIE, "Proto_Indo_European", LanguageGroup.Special,  new Dictionary<LetterKey, string[]>
        {
            [LetterKey.VK_A] = ["ā"],
            [LetterKey.VK_E] = ["ē"],
            [LetterKey.VK_O] = ["ō"],
            [LetterKey.VK_K] = ["ḱ"],
            [LetterKey.VK_G] = ["ǵ"],
            [LetterKey.VK_R] = ["r̥"],
            [LetterKey.VK_L] = ["l̥"],
            [LetterKey.VK_M] = ["m̥"],
            [LetterKey.VK_N] = ["n̥"],
        }),

        new(Language.PL, "Polish", LanguageGroup.Language, new Dictionary<LetterKey, string[]>
        {
            [LetterKey.VK_A] = ["ą"],
            [LetterKey.VK_C] = ["ć"],
            [LetterKey.VK_E] = ["ę", "€"],
            [LetterKey.VK_L] = ["ł"],
            [LetterKey.VK_N] = ["ń"],
            [LetterKey.VK_O] = ["ó"],
            [LetterKey.VK_S] = ["ś"],
            [LetterKey.VK_Z] = ["ż", "ź"],
            [LetterKey.VK_COMMA] = ["„", "”", "‘", "’", "»", "«"],
        }),

        new(Language.PT, "Portuguese", LanguageGroup.Language, new Dictionary<LetterKey, string[]>
        {
            [LetterKey.VK_A] = ["á", "à", "â", "ã", "ª"],
            [LetterKey.VK_C] = ["ç"],
            [LetterKey.VK_E] = ["é", "ê", "€"],
            [LetterKey.VK_I] = ["í"],
            [LetterKey.VK_O] = ["ô", "ó", "õ", "º"],
            [LetterKey.VK_S] = ["$"],
            [LetterKey.VK_U] = ["ú"],
            [LetterKey.VK_COMMA] = ["“", "”", "‘", "’", "«", "»"],
        }),

        new(Language.RO, "Romanian", LanguageGroup.Language, new Dictionary<LetterKey, string[]>
        {
            [LetterKey.VK_A] = ["ă", "â"],
            [LetterKey.VK_I] = ["î"],
            [LetterKey.VK_S] = ["ș"],
            [LetterKey.VK_T] = ["ț"],
            [LetterKey.VK_COMMA] = ["„", "”", "«", "»"],
        }),

        // Middle Eastern Romanization. This is a "special" language group as it's not a
        // spoken language, but rather a set of characters used to romanize various Middle
        // Eastern languages.
        new(Language.ROM, "Romanization", LanguageGroup.Special,  new Dictionary<LetterKey, string[]>
        {
            [LetterKey.VK_A] = ["á", "â", "ă", "ā"],
            [LetterKey.VK_B] = ["ḇ"],
            [LetterKey.VK_C] = ["č", "ç"],
            [LetterKey.VK_D] = ["ḑ", "ḍ", "ḏ", "ḏ\u0323"],
            [LetterKey.VK_E] = ["ê", "ě", "ĕ", "ē", "é", "ə"],
            [LetterKey.VK_G] = ["ġ", "ǧ", "ğ", "ḡ", "g\u0303", "g\u0331"],
            [LetterKey.VK_H] = ["ḧ", "ḩ", "ḥ", "ḫ", "h\u0331"],
            [LetterKey.VK_I] = ["í", "ı", "î", "ī", "ı\u0307\u0304"],
            [LetterKey.VK_J] = ["ǰ", "j\u0331"],
            [LetterKey.VK_K] = ["ḳ", "ḵ"],
            [LetterKey.VK_L] = ["ł"],
            [LetterKey.VK_N] = ["ⁿ", "ñ"],
            [LetterKey.VK_O] = ["ó", "ô", "ö", "ŏ", "ō", "ȫ"],
            [LetterKey.VK_P] = ["p\u0304"],
            [LetterKey.VK_R] = ["ṙ", "ṛ"],
            [LetterKey.VK_S] = ["ś", "š", "ş", "ṣ", "s\u0331", "ṣ\u0304"],
            [LetterKey.VK_T] = ["ẗ", "ţ", "ṭ", "ṯ"],
            [LetterKey.VK_U] = ["ú", "û", "ü", "ū", "ǖ"],
            [LetterKey.VK_V] = ["v\u0307", "ṿ", "ᵛ"],
            [LetterKey.VK_Y] = ["̀y"],
            [LetterKey.VK_Z] = ["ż", "ž", "z\u0304", "z\u0327", "ẓ", "z\u0324", "ẕ"],
            [LetterKey.VK_PERIOD] = ["’", "ʾ", "ʿ", "′", "…"],
        }),

        new(Language.SK, "Slovak", LanguageGroup.Language, new Dictionary<LetterKey, string[]>
        {
            [LetterKey.VK_A] = ["á", "ä"],
            [LetterKey.VK_C] = ["č"],
            [LetterKey.VK_D] = ["ď"],
            [LetterKey.VK_E] = ["é", "€"],
            [LetterKey.VK_I] = ["í"],
            [LetterKey.VK_L] = ["ľ", "ĺ"],
            [LetterKey.VK_N] = ["ň"],
            [LetterKey.VK_O] = ["ó", "ô"],
            [LetterKey.VK_R] = ["ŕ"],
            [LetterKey.VK_S] = ["š"],
            [LetterKey.VK_T] = ["ť"],
            [LetterKey.VK_U] = ["ú"],
            [LetterKey.VK_Y] = ["ý"],
            [LetterKey.VK_Z] = ["ž"],
            [LetterKey.VK_COMMA] = ["„", "“", "‚", "‘", "»", "«", "›", "‹"],
        }),

        new(Language.SL, "Slovenian", LanguageGroup.Language, new Dictionary<LetterKey, string[]>
        {
            [LetterKey.VK_C] = ["č", "ć"],
            [LetterKey.VK_E] = ["€"],
            [LetterKey.VK_S] = ["š"],
            [LetterKey.VK_Z] = ["ž"],
            [LetterKey.VK_COMMA] = ["„", "“", "»", "«"],
        }),

        new(Language.SP, "Spanish", LanguageGroup.Language, new Dictionary<LetterKey, string[]>
        {
            [LetterKey.VK_A] = ["á"],
            [LetterKey.VK_E] = ["é", "€"],
            [LetterKey.VK_H] = ["ḥ"],
            [LetterKey.VK_I] = ["í"],
            [LetterKey.VK_L] = ["ḷ"],
            [LetterKey.VK_N] = ["ñ"],
            [LetterKey.VK_O] = ["ó"],
            [LetterKey.VK_U] = ["ú", "ü"],
            [LetterKey.VK_COMMA] = ["¿", "?", "¡", "!", "«", "»", "“", "”", "‘", "’"],
        }),

        new(Language.SR, "Serbian", LanguageGroup.Language, new Dictionary<LetterKey, string[]>
        {
            [LetterKey.VK_C] = ["ć", "č"],
            [LetterKey.VK_D] = ["đ"],
            [LetterKey.VK_S] = ["š"],
            [LetterKey.VK_Z] = ["ž"],
            [LetterKey.VK_COMMA] = ["„", "“", "‚", "’", "»", "«", "›", "‹"],
        }),

        new(Language.SR_CYRL, "Serbian_Cyrillic", LanguageGroup.Language, new Dictionary<LetterKey, string[]>
        {
            [LetterKey.VK_D] = ["ђ", "џ"],
            [LetterKey.VK_L] = ["љ"],
            [LetterKey.VK_N] = ["њ"],
            [LetterKey.VK_C] = ["ћ"],
        }),

        new(Language.SV, "Swedish", LanguageGroup.Language, new Dictionary<LetterKey, string[]>
        {
            [LetterKey.VK_A] = ["å", "ä"],
            [LetterKey.VK_E] = ["é"],
            [LetterKey.VK_O] = ["ö"],
            [LetterKey.VK_COMMA] = ["”", "’", "»", "«"],
        }),

        new(Language.TK, "Turkish", LanguageGroup.Language, new Dictionary<LetterKey, string[]>
        {
            [LetterKey.VK_A] = ["â"],
            [LetterKey.VK_C] = ["ç"],
            [LetterKey.VK_E] = ["ë", "€"],
            [LetterKey.VK_G] = ["ğ"],
            [LetterKey.VK_I] = ["ı", "İ", "î",],
            [LetterKey.VK_O] = ["ö", "ô"],
            [LetterKey.VK_S] = ["ş"],
            [LetterKey.VK_T] = ["₺"],
            [LetterKey.VK_U] = ["ü", "û"],
            [LetterKey.VK_COMMA] = ["“", "”", "‘", "’", "«", "»", "‹", "›"],
        }),

        new(Language.VI, "Vietnamese", LanguageGroup.Language, new Dictionary<LetterKey, string[]>
        {
            [LetterKey.VK_A] = ["à", "ả", "ã", "á", "ạ", "ă", "ằ", "ẳ", "ẵ", "ắ", "ặ", "â", "ầ", "ẩ", "ẫ", "ấ", "ậ"],
            [LetterKey.VK_D] = ["đ"],
            [LetterKey.VK_E] = ["è", "ẻ", "ẽ", "é", "ẹ", "ê", "ề", "ể", "ễ", "ế", "ệ"],
            [LetterKey.VK_I] = ["ì", "ỉ", "ĩ", "í", "ị"],
            [LetterKey.VK_O] = ["ò", "ỏ", "õ", "ó", "ọ", "ô", "ồ", "ổ", "ỗ", "ố", "ộ", "ơ", "ờ", "ở", "ỡ", "ớ", "ợ"],
            [LetterKey.VK_U] = ["ù", "ủ", "ũ", "ú", "ụ", "ư", "ừ", "ử", "ữ", "ứ", "ự"],
            [LetterKey.VK_Y] = ["ỳ", "ỷ", "ỹ", "ý", "ỵ"],
        }),
    ];

    /// <summary>
    /// O(1) lookup from <see cref="Language"/> to its <see cref="LanguageInfo"/>.
    /// Use this instead of searching <see cref="All"/> when you have a language identity.
    /// </summary>
    public static readonly IReadOnlyDictionary<Language, LanguageInfo> LanguageLookup =
        All.ToDictionary(x => x.Id);

    /// <summary>
    /// The order in which language groups appear in the Quick Accent popup.
    /// Groups listed first have their characters shown first.
    /// This is intentionally separate from the <see cref="LanguageGroup"/> enum order.
    /// </summary>
    public static readonly IReadOnlyList<LanguageGroup> GroupDisplayOrder =
    [
        LanguageGroup.UserDefined,
        LanguageGroup.Language,
        LanguageGroup.Special,
    ];

    /// <summary>
    /// The order in which individual languages appear within their group in the Quick
    /// Accent popup. Position in this list is the display order; position in
    /// <see cref="All"/> is irrelevant for popup ordering.
    /// Entries are sorted alphabetically by <see cref="Language"/> enum name.
    /// When adding a new language, insert it in alphabetical order.
    /// </summary>
    public static readonly IReadOnlyList<Language> DisplayOrder =
    [

        // Spoken languages.
        Language.BG,
        Language.CA,
        Language.CRH,
        Language.CY,
        Language.CZ,
        Language.DE,
        Language.DK,
        Language.EL,
        Language.EPO,
        Language.EST,
        Language.FI,
        Language.FR,
        Language.GA,
        Language.GD,
        Language.HE,
        Language.HR,
        Language.HU,
        Language.IS,
        Language.IT,
        Language.KU,
        Language.LT,
        Language.MI,
        Language.MK,
        Language.MT,
        Language.NL,
        Language.NO,
        Language.PI,
        Language.PL,
        Language.PT,
        Language.RO,
        Language.SK,
        Language.SL,
        Language.SP,
        Language.SR,
        Language.SR_CYRL,
        Language.SV,
        Language.TK,
        Language.VI,

        // Symbols, non-spoken languages, and non-language-specific characters.
        Language.CUR,
        Language.GRC,
        Language.IPA,
        Language.PIE,
        Language.ROM,
        Language.SPECIAL,
    ];

    // O(1) sort-key lookups derived from the display order lists above.
    private static readonly Dictionary<LanguageGroup, int> _groupOrder =
        GroupDisplayOrder.Select((g, i) => (g, i)).ToDictionary(x => x.g, x => x.i);

    private static readonly Dictionary<Language, int> _languageOrder =
        DisplayOrder.Select((l, i) => (l, i)).ToDictionary(x => x.l, x => x.i);

    private static readonly ConcurrentDictionary<LetterKey, string[]> _allLanguagesCache = new();

    /// <summary>
    /// Returns the deduplicated set of characters for the given key across the specified
    /// languages, ordered by <see cref="GroupDisplayOrder"/> then <see cref="DisplayOrder"/>.
    /// </summary>
    public static string[] GetCharacters(LetterKey letter, Language[] langs)
    {
        if (langs.Length == 0)
        {
            return [];
        }

        if (langs.Length == All.Count)
        {
            return _allLanguagesCache.GetOrAdd(letter, key => Collect(key, All));
        }

        return Collect(letter, langs.Select(lang => LanguageLookup[lang]));
    }

    private static string[] Collect(LetterKey letter, IEnumerable<LanguageInfo> maps)
    {
        var result = new List<string>();
        foreach (var map in maps
            .OrderBy(m => _groupOrder[m.Group])
            .ThenBy(m => _languageOrder[m.Id]))
        {
            if (map.Characters.TryGetValue(letter, out var chars))
            {
                result.AddRange(chars);
            }
        }

        // Stable-sort: letters and diacritics before symbols/currency.
        // This prevents symbol-only entries (e.g. "€" from German) from
        // appearing ahead of actual accented letters (e.g. "é" from Spanish)
        // when languages are aggregated alphabetically.
        return [.. result.Distinct().OrderBy(c => IsSymbolOrCurrency(c) ? 1 : 0)];
    }

    /// <summary>
    /// Returns true if the first character of the string is a symbol or currency
    /// character rather than a letter, mark, or digit.
    /// </summary>
    private static bool IsSymbolOrCurrency(string s)
    {
        if (string.IsNullOrEmpty(s))
        {
            return false;
        }

        var category = char.GetUnicodeCategory(s[0]);
        return category is System.Globalization.UnicodeCategory.CurrencySymbol
            or System.Globalization.UnicodeCategory.MathSymbol
            or System.Globalization.UnicodeCategory.OtherSymbol
            or System.Globalization.UnicodeCategory.ModifierSymbol;
    }
}
