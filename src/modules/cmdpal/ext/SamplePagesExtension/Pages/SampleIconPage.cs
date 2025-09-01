// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Linq;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace SamplePagesExtension.Pages;

internal sealed partial class SampleIconPage : ListPage
{
    private readonly IListItem[] _items =
    [
        /*
         * Quick intro to Unicode in source code:
         * - Every character has a code point (e.g., U+0041 = 'A').
         * - Code points up to U+FFFF use \uXXXX (4 hex digits and lowercase u).
         * - Code points above that (up to U+10FFFF) use \UXXXXXXXX (8 hex digits and capital letter U).
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
        BuildIconItem("1", "Simple text character as icon", "Basic letter character used as an icon demonstration"),

        // Emoji Keycap Digit Two ... 2️⃣
        // Unicode: \U00000032\U0000FE0F\U000020E3
        // This is a sequence of three code points: the digit '2' (U+0032), a variation selector (U+FE0F) to specify emoji presentation, and a combining enclosing keycap (U+20E3).
        BuildIconItem("2️⃣", "Emoji with variation selector", "Emoji character using a variation selector to specify emoji presentation"),

        // Symbol #
        // Unicode: \u0023
        BuildIconItem("#", "Simple text character as icon", "Basic letter character used as an icon demonstration"),

        // Symbol #
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

        // mahjong tile 🀙 (non-emoji)
        // https://en.wikipedia.org/wiki/Mahjong_Tiles_(Unicode_block)
        // Unicode: \U0001F019
        BuildIconItem("\U0001F019", "Mahjong tile non-emoji", "Mahjong tile character that is not classified as an emoji"),
    ];

    public SampleIconPage()
    {
        Icon = new IconInfo("\uE8BA");
        Name = "Sample Icon Page";
        ShowDetails = true;
    }

    public override IListItem[] GetItems() => _items;

    private static ListItem BuildIconItem(string icon, string title, string description)
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
                    }
                ],
            },
        };
    }
}
