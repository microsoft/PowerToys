// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Linq;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace SamplePagesExtension.Pages;

internal sealed partial class SampleIconPage : ListPage
{
    private const string PlainSvgSample = """
        <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 32 32">
          <rect x="1" y="1" width="30" height="30" rx="8" fill="#E8DEF8" />
          <path d="M9 16l5 5 9-11" fill="none" stroke="#7A3E9D" stroke-width="3" stroke-linecap="round" stroke-linejoin="round" />
        </svg>
        """;

    private const string ThemedSvgSample = """
        <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 20 20" color="{{ThemeColor}}">
          <path
            fill="currentColor"
            d="M10 18q.128 0 .254-.004a5.5 5.5 0 0 1-.698-1.083c-.536-.207-1.098-.793-1.578-1.821A9.3 9.3 0 0 1 7.42 13.5h1.672q.096-.52.284-1h-2.17A15 15 0 0 1 7 10c0-.883.073-1.725.206-2.5h5.588c.092.541.156 1.115.186 1.713q.48-.138.992-.188a16 16 0 0 0-.165-1.525h2.733c.251.656.406 1.36.448 2.094q.543.276 1.008.66A8 8 0 1 0 10 18M10 3c.657 0 1.407.59 2.022 1.908.217.466.406 1.002.559 1.592H7.419c.153-.59.342-1.126.56-1.592C8.592 3.59 9.342 3 10 3M7.072 4.485A10.5 10.5 0 0 0 6.389 6.5H3.936a7.02 7.02 0 0 1 3.778-3.118c-.241.33-.456.704-.642 1.103M6.192 7.5A16 16 0 0 0 6 10c0 .87.067 1.712.193 2.5H3.46A7 7 0 0 1 3 10c0-.88.163-1.724.46-2.5zm.197 6c.176.743.407 1.422.683 2.015c.186.399.401.773.642 1.103A7.02 7.02 0 0 1 3.936 13.5zm5.897-10.118A7.02 7.02 0 0 1 16.064 6.5H13.61a10.5 10.5 0 0 0-.683-2.015 6.6 6.6 0 0 0-.642-1.103" />
          <path
            fill="{{AccentColor}}"
            d="M19 14.5a4.5 4.5 0 1 1-9 0 4.5 4.5 0 0 1 9 0m-4.854-2.353-2 2a.5.5 0 0 0 .708.707L14 13.707V16.5a.5.5 0 0 0 1 0v-2.793l1.146 1.147a.5.5 0 0 0 .708-.708l-2-2a.5.5 0 0 0-.351-.146h-.006a.5.5 0 0 0-.348.144z" />
        </svg>
        """;

    private readonly IListItem[] _items =
    [
        BuildProtocolIconItem(
            "|Initials|A|#FF7A3E9D|circle|",
            "Generated circular initials avatar",
            "Uses an automatically contrasting foreground"),

        BuildProtocolIconItem(
            "|Initials|CP|#FF005FB8|#FF60CDFF|square|",
            "Theme-aware square initials avatar",
            "Uses separate light and dark background colors"),

        BuildProtocolIconItem(
            "|Initials|Å|info|circle|",
            "Accented Latin initials avatar",
            "Uses DirectWrite font fallback and vector outlines"),

        BuildProtocolIconItem(
            "|Initials|ЖП|success|square|",
            "Cyrillic initials avatar",
            "Shapes non-Latin initials off the UI thread"),

        BuildProtocolIconItem(
            "|Initials|東京|warning|circle|",
            "CJK initials avatar",
            "Uses the installed Windows font selected by system fallback"),

        BuildProtocolIconItem(
            "|Initials|👩‍💻|#FF7A3E9D|square|",
            "Multi-code-point initials avatar",
            "Treats a joined emoji sequence as one Unicode text element"),

        BuildProtocolIconItem(
            "|Initials|A%7CB|neutral|circle|",
            "Escaped separator initials avatar",
            "Uses %7C for a literal protocol separator"),

        BuildProtocolIconItem(
            "|Initials|%25|subtle|square|",
            "Escaped percent initials avatar",
            "Uses %25 for a literal percent sign"),

        BuildProtocolIconItem(
            "|Initials|N|normal|circle|",
            "Normal-color initials avatar",
            "Uses the normal theme foreground as its background color"),

        BuildProtocolIconItem(
            "|Initials|T|transparent|square|",
            "Transparent initials icon",
            "Omits the visible background while keeping theme-contrasting initials"),

        BuildProtocolIconItem(
            "|Swatch|#FF7A3E9D|circle|",
            "Generated circular color swatch",
            "Uses one literal color in both themes"),

        BuildProtocolIconItem(
            "|Swatch|#FF005FB8|#FF60CDFF|square|",
            "Theme-aware square color swatch",
            "Uses separate light and dark colors"),

        BuildProtocolIconItem("|Swatch|danger|circle|", "Semantic danger swatch", "Uses the theme-aware danger color"),
        BuildProtocolIconItem("|Swatch|subtle|square|", "Semantic subtle swatch", "Uses the theme-aware subtle color"),
        BuildProtocolIconItem("|Swatch|info|circle|", "Semantic info swatch", "Uses the theme-aware info color"),
        BuildProtocolIconItem("|Swatch|warning|square|", "Semantic warning swatch", "Uses the theme-aware warning color"),
        BuildProtocolIconItem("|Swatch|success|circle|", "Semantic success swatch", "Uses the theme-aware success color"),
        BuildProtocolIconItem("|Swatch|neutral|square|", "Semantic neutral swatch", "Uses the theme-aware neutral color"),
        BuildProtocolIconItem("|Swatch|dark|circle|", "Semantic dark swatch", "Uses the same deliberately dark color in both themes"),
        BuildProtocolIconItem("|Swatch|normal|square|", "Semantic normal swatch", "Uses the normal foreground color for the current theme"),
        BuildProtocolIconItem("|Swatch|transparent|circle|", "Transparent swatch", "Intentionally renders no visible fill; primarily useful as an initials background"),

        BuildProtocolIconItem(
            "|Svg|" + PlainSvgSample,
            "Plain inline SVG",
            "Passes SVG content through without theme expansion"),

        BuildProtocolIconItem(
            "|ThemedSvg|success|" + ThemedSvgSample,
            "Themed inline SVG",
            "Uses currentColor for the globe and a semantic success accent for the overlay"),

        /*
         * Quick intro to Unicode in source code:
         * - Every character has a code point (e.g., U+0041 = 'A').
         * - Code points up to U+FFFF use \u1234 (4 hex digits and lowercase u).
         * - Code points above that (up to U+10FFFF) use \U12345678 (8 hex digits and capital letter U).
         * - If your source file is UTF-8, you can type the character directly, but it may not display properly in editors,
         *   and it's harder to see the actual code point.
         * - Some symbols (like many emojis) are built from multiple code points
         *   joined together (e.g., 👋🏻 = U+1F44B + U+1F3FB).
         *
         * Examples:
         *   😍 = "😍" or "\U0001F60D"
         *   👋🏻 = "👋🏻" or "\U0001F44B\U0001F3FB"
         *   🧙‍♂️ = "🧙‍♂️" or "\U0001F9D9\u200D\u2642\U0000FE0F"   (male mage)
         *   🧙🏿‍♀️ = "🧙🏿‍♀️" or "\U0001F9D9\U0001F3FF\u200D\u2640\U0000FE0F" (dark-skinned woman mage)
         *
         */

        // Emoji Smiling Face with Heart-Eyes
        // Unicode: \U0001F60D
        BuildIconItem("😍", "Standard emoji icon", "Basic emoji character rendered as an icon"),

        // Emoji Smiling Face with Heart-Eyes
        // Unicode: \U0001F60D\U0001F643\U0001F622
        BuildIconItem("😍🙃😢", "Multiple emojis", "Use of multiple emojis for icon is not allowed"),

        // Emoji Smiling Face with Sunglasses
        // Unicode: \U0001F60E
        BuildIconItem("\U0001F60E", "Unicode escape sequence emoji", "Emoji defined using Unicode escape sequence notation"),

        // Segoe Fluent Icons font icon
        // Unicode: \uE8D4
        BuildIconItem("\uE8D4", "Segoe Fluent icon demonstration", "Segoe Fluent/MDL2 icon from system font\nWorks as an icon but won't display properly in button text"),

        // Extended pictographic symbol for keyboard
        BuildIconItem("\u2328", "Extended pictographic symbol", "Pictographic symbol representing a keyboard"),

        // Capital letter A
        BuildIconItem("A", "Simple text character as icon", "Basic letter character used as an icon demonstration"),

        // Letter 1
        // Unicode: \U00000031
        BuildIconItem("1", "Simple text character as icon", "Basic letter character used as an icon demonstration"),

        // Emoji Keycap Digit Two ... 2️⃣
        // Unicode: \U00000032\U000020E3
        // This is a sequence of three code points: the digit '2' (U+0032), and a combining enclosing keycap (U+20E3). No variation selector is used here.
        BuildIconItem("\U00000032\U000020E3", "Emoji without variation selector", "Emoji character doesn't have VS16 variation selector to render as text"),

        // Emoji Keycap Digit Three ... 3️⃣
        // Unicode: \U00000033\U0000FE0F\U000020E3
        // This is a sequence of three code points: the digit '3' (U+0033), a variation selector (U+FE0F) to specify emoji presentation, and a combining enclosing keycap (U+20E3).
        BuildIconItem("3️⃣", "Emoji with variation selector", "Emoji character using a variation selector to specify emoji presentation"),

        // Symbol #
        // Unicode: \u0023
        BuildIconItem("#", "Simple text character as icon", "Basic letter character used as an icon demonstration"),

        // Symbol # keycap
        // Unicode: \u0023\ufe0f\u20e3
        // Sequence of 3 code points: symbol #, a variation selector (U+FE0F) to specify emoji presentation, and a combining enclosing keycap (U+20E3).
        BuildIconItem("\u0023\ufe0f\u20e3", "Simple text character as icon", "Basic letter character used as an icon demonstration"),

        // Capital letter WM
        // This is two characters, which is not a valid icon representation. It will be replaced by a placeholder signalizing an invalid icon.
        BuildIconItem("WM", "Invalid icon representation", "String with multiple characters that does not correspond to a valid single icon"),

        // Emoji Mage
        // Unicode: \U0001F9D9
        BuildIconItem("🧙", "Single code-point emoji example", "Simple emoji character using a single Unicode code point"),

        // Emoji Male Mage (Mage with gender modifier)
        // Unicode: \U0001F9D9\u200D\u2642\uFE0F
        BuildIconItem("🧙‍♂️", "Complex emoji with gender modifier", "Composite emoji using Zero-Width Joiner (ZWJ) sequence for male variant"),

        // Emoji Woman Mage (Mage with gender modifier)
        // Unicode: \U0001F9D9\u200D\u2640\uFE0F
        BuildIconItem("\U0001F9D9\u200D\u2640\uFE0F", "Complex emoji with gender modifier", "Composite emoji using Zero-Width Joiner (ZWJ) sequence for female variant"),

        // Emoji Waving Hand
        // Unicode: \U0001F44B
        BuildIconItem("👋", "Basic hand gesture emoji", "Standard emoji character representing a waving hand"),

        // Emoji Waving Hand + Light Skin Tone
        // Unicode: \U0001F44B\U0001F3FB
        BuildIconItem("👋🏻", "Emoji with light skin tone modifier", "Emoji enhanced with Unicode skin tone modifier (light)"),

        // Emoji Waving Hand + Dark Skin Tone
        // Unicode: \U0001F44B\U0001F3FF
        BuildIconItem("\U0001F44B\U0001F3FF", "Emoji with dark skin tone modifier", "Emoji enhanced with Unicode skin tone modifier (dark)"),

        // Flag of Czechia (Czech Republic)
        // Unicode: \U0001F1E8\U0001F1FF
        BuildIconItem("\U0001F1E8\U0001F1FF", "Flag emoji using regional indicators", "Emoji flag constructed from regional indicator symbols for Czechia"),

        // Use of ZWJ without emojis
        // KA (\u0995) + VIRAMA (\u09CD) + ZWJ (\u200D) - shows the half-form KA
        // Unicode: \u0995\u09CD\u200D
        BuildIconItem("\u0995\u09CD\u200D", "Use of ZWJ in non-emoji context", "Shows the half-form KA"),

        // Use of ZWJ without emojis
        // KA (\u0995) + VIRAMA (\u09CD) + Shows full KA with an explicit virama mark (not half-form).
        // Unicode: \u0995\u09CD
        BuildIconItem("\u0995\u09CD", "Use of ZWJ in non-emoji context", "Shows full KA with an explicit virama mark"),

        // mahjong tile red dragon (using Unicode escape sequence)
        // https://en.wikipedia.org/wiki/Mahjong_Tiles_(Unicode_block)
        // Unicode: \U0001F004
        BuildIconItem("\U0001F004", "Mahjong tile emoji (red dragon)", "Mahjong tile red dragon emoji character using Unicode escape sequence"),

        // mahjong tile green dragon (non-emoji)
        // https://en.wikipedia.org/wiki/Mahjong_Tiles_(Unicode_block)
        // Unicode: \U0001F005
        BuildIconItem("\U0001F005", "Mahjong tile non-emoji (green dragon)", "Mahjong tile character that is not classified as an emoji"),

        // Play, PlayPause, Stop
        BuildIconItem("\u25B6", "Play symbol (standalone)", "Play symbol"),
        BuildIconItem("\u25B6\uFE0E", "Play symbol + VS15 (request text)", "Play symbol with variation specifier requesting rendering as text"),
        BuildIconItem("\u25B6\uFE0F", "Play symbol + VS16 (request emoji)", "Play symbol with variation specifier requesting rendering as emoji "),
        BuildIconItem("⏯️", "Play/Pause keycap emoji", "Play/Pause keycap emoji doesn't have plain text variant"),
        BuildIconItem("⏸️", "Pause keycap emoji", "Pause keycap emoji doesn't have plain text variant"),

        // Copyright and emoji copyright:
        BuildIconItem("\u00a9", "Copyright symbol (standalone)", "Copyright symbol that is not classified as an emoji"),
        BuildIconItem("\u00a9\uFE0E", "Copyright symbol + VS15 (request text)", "Copyright symbol that is not classified as an emoji"),
        BuildIconItem("\u00a9\uFE0F", "Copyright symbol + VS16 (request emoji)", "Copyright symbol that is not classified as an emoji"),

        // Tag flags
        BuildIconItem("🏳️", "White Flag", "White Flag"),
        BuildIconItem("\U0001F3F4\u200D\u2620\uFE0F", "Pirate Flag", "Pirate Flag"),
    ];

    public SampleIconPage()
    {
        Icon = new IconInfo("\uE8BA");
        Name = "Sample Icon Page";
        ShowDetails = true;
    }

    public override IListItem[] GetItems() => _items;

    private static ListItem BuildProtocolIconItem(string icon, string title, string description) =>
        BuildIconItem(
            icon,
            title,
            description,
            new DetailsElement
            {
                Key = "Icon Protocol",
                Data = new DetailsTags
                {
                    Tags = [new Tag(icon)],
                },
            });

    private static ListItem BuildIconItem(string icon, string title, string description) =>
        BuildIconItem(
            icon,
            title,
            description,
            new DetailsElement
            {
                Key = "Unicode Code Points",
                Data = new DetailsTags
                {
                    Tags = icon.EnumerateRunes()
                        .Select(rune => rune.Value <= 0xFFFF ? $"\\u{rune.Value:X4}" : $"\\U{rune.Value:X8}")
                        .Select(t => new Tag(t))
                        .ToArray<ITag>(),
                },
            });

    private static ListItem BuildIconItem(
        string icon,
        string title,
        string description,
        DetailsElement metadata)
    {
        var iconInfo = new IconInfo(icon);

        return new ListItem(new CopyTextCommand(icon) { Name = "Action with " + icon })
        {
            Title = title,
            Subtitle = description,
            Icon = iconInfo,
            Tags = [
                new Tag("Tag") { Icon = iconInfo },
            ],
            Details = new Details
            {
                HeroImage = iconInfo,
                Title = title,
                Body = description,
                Metadata = [
                    metadata,
                ],
            },
        };
    }
}
