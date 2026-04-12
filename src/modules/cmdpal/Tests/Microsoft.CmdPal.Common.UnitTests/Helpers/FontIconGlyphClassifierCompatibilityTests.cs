// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;
using System.Text;
using ManagedGlyphClassifier = Microsoft.CmdPal.Common.Helpers.FontIconGlyphClassifier;
using ManagedGlyphKind = Microsoft.CmdPal.Common.Helpers.FontIconGlyphKind;
using NativeGlyphClassifier = Microsoft.Terminal.UI.FontIconGlyphClassifier;
using NativeGlyphKind = Microsoft.Terminal.UI.FontIconGlyphKind;

namespace Microsoft.CmdPal.Common.UnitTests.Helpers;

[TestClass]
public class FontIconGlyphClassifierCompatibilityTests
{
    private static readonly string[] EmojiSequences =
    [
        "😀",
        "👨‍👩‍👧‍👦",
        "👩🏽‍💻",
        "🇺🇸",
        "🏳️‍🌈",
        "🏴‍☠️",
        "❤️",
        "♥︎",
        "1️⃣",
        "#️⃣",
        "*️⃣",
        "👩‍❤️‍👨",
        "🧑‍🧑‍🧒",
    ];

    private static readonly string[] Words =
    [
        "PowerToys",
        "CmdPal",
        "Windows",
        "hello",
        "world",
        "naïve",
        "façade",
        "résumé",
        "日本語",
        "Русский",
        "العربية",
        "emoji123",
        @"C:\Temp\icon.png",
        "ms-appx:///Assets/Icons/ExtensionIconPlaceholder.png",
    ];

    private static readonly (int Start, int End)[] EmojiPresentationRanges =
    [
        (0x231A, 0x231B),
        (0x23E9, 0x23EC),
        (0x23F0, 0x23F0),
        (0x23F3, 0x23F3),
        (0x25FD, 0x25FE),
        (0x2614, 0x2615),
        (0x2648, 0x2653),
        (0x267F, 0x267F),
        (0x2693, 0x2693),
        (0x26A1, 0x26A1),
        (0x26AA, 0x26AB),
        (0x26BD, 0x26BE),
        (0x26C4, 0x26C5),
        (0x26CE, 0x26CE),
        (0x26D4, 0x26D4),
        (0x26EA, 0x26EA),
        (0x26F2, 0x26F3),
        (0x26F5, 0x26F5),
        (0x26FA, 0x26FA),
        (0x26FD, 0x26FD),
        (0x2705, 0x2705),
        (0x270A, 0x270B),
        (0x2728, 0x2728),
        (0x274C, 0x274C),
        (0x274E, 0x274E),
        (0x2753, 0x2755),
        (0x2757, 0x2757),
        (0x2795, 0x2797),
        (0x27B0, 0x27B0),
        (0x27BF, 0x27BF),
        (0x2B1B, 0x2B1C),
        (0x2B50, 0x2B50),
        (0x2B55, 0x2B55),
        (0x1F004, 0x1F004),
        (0x1F0CF, 0x1F0CF),
        (0x1F18E, 0x1F18E),
        (0x1F191, 0x1F19A),
        (0x1F1E6, 0x1F1FF),
        (0x1F201, 0x1F201),
        (0x1F21A, 0x1F21A),
        (0x1F22F, 0x1F22F),
        (0x1F232, 0x1F236),
        (0x1F238, 0x1F23A),
        (0x1F250, 0x1F251),
        (0x1F300, 0x1F321),
        (0x1F324, 0x1F393),
        (0x1F396, 0x1F397),
        (0x1F399, 0x1F39B),
        (0x1F39E, 0x1F3F0),
        (0x1F3F3, 0x1F3F5),
        (0x1F3F7, 0x1F4FD),
        (0x1F4FF, 0x1F53D),
        (0x1F54B, 0x1F54E),
        (0x1F550, 0x1F567),
        (0x1F57A, 0x1F57A),
        (0x1F595, 0x1F596),
        (0x1F5A4, 0x1F5A4),
        (0x1F5FB, 0x1F64F),
        (0x1F680, 0x1F6C5),
        (0x1F6CC, 0x1F6CC),
        (0x1F6D0, 0x1F6D2),
        (0x1F6D5, 0x1F6D7),
        (0x1F6DC, 0x1F6DC),
        (0x1F6EB, 0x1F6EC),
        (0x1F6F4, 0x1F6FC),
        (0x1F7E0, 0x1F7EB),
        (0x1F90C, 0x1F93A),
        (0x1F93C, 0x1F945),
        (0x1F947, 0x1F9FF),
        (0x1FA70, 0x1FA7C),
        (0x1FA80, 0x1FA89),
        (0x1FA8F, 0x1FA8F),
        (0x1FA90, 0x1FABD),
        (0x1FABF, 0x1FAC5),
        (0x1FACE, 0x1FADB),
        (0x1FAE0, 0x1FAE8),
        (0x1FAF0, 0x1FAF8),
    ];

    [TestMethod]
    public void Classify_MatchesNativeClassifier_ForBruteForceCorpus()
    {
        List<string>? mismatches = null;

        foreach (var input in BuildBruteForceCorpus())
        {
            var expected = ConvertKind(NativeGlyphClassifier.Classify(input));
            var actual = ManagedGlyphClassifier.Classify(input);
            if (expected != actual)
            {
                mismatches ??= [];
                mismatches.Add($"Classification mismatch for {DescribeInput(input)}. Expected {expected}, actual {actual}.");
            }
        }

        if (mismatches is not null)
        {
            Assert.Fail(string.Join(Environment.NewLine, mismatches.Take(50)));
        }
    }

    [TestMethod]
    public void IsLikelyToBeEmojiOrSymbolIcon_MatchesNativeClassifier_ForBruteForceCorpus()
    {
        List<string>? mismatches = null;

        foreach (var input in BuildBruteForceCorpus())
        {
            var expected = NativeGlyphClassifier.IsLikelyToBeEmojiOrSymbolIcon(input);
            var actual = ManagedGlyphClassifier.IsLikelyToBeEmojiOrSymbolIcon(input);
            if (expected != actual)
            {
                mismatches ??= [];
                mismatches.Add($"Likelihood mismatch for {DescribeInput(input)}. Expected {expected}, actual {actual}.");
            }
        }

        if (mismatches is not null)
        {
            Assert.Fail(string.Join(Environment.NewLine, mismatches.Take(50)));
        }
    }

    private static IEnumerable<string> BuildBruteForceCorpus()
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var input in GetAsciiLetters())
        {
            if (seen.Add(input))
            {
                yield return input;
            }
        }

        foreach (var input in GetNumbers())
        {
            if (seen.Add(input))
            {
                yield return input;
            }
        }

        foreach (var input in GetAsciiSymbols())
        {
            if (seen.Add(input))
            {
                yield return input;
            }
        }

        foreach (var input in GetNonAsciiSymbols())
        {
            if (seen.Add(input))
            {
                yield return input;
            }
        }

        foreach (var input in GetSingleCodePointEmoji())
        {
            if (seen.Add(input))
            {
                yield return input;
            }
        }

        foreach (var input in EmojiSequences)
        {
            if (seen.Add(input))
            {
                yield return input;
            }
        }

        foreach (var input in Words)
        {
            if (seen.Add(input))
            {
                yield return input;
            }
        }
    }

    private static IEnumerable<string> GetAsciiLetters()
    {
        for (var c = 'A'; c <= 'Z'; c++)
        {
            yield return c.ToString();
        }

        for (var c = 'a'; c <= 'z'; c++)
        {
            yield return c.ToString();
        }
    }

    private static IEnumerable<string> GetNumbers()
    {
        for (var c = '0'; c <= '9'; c++)
        {
            yield return c.ToString();
        }
    }

    private static IEnumerable<string> GetAsciiSymbols()
    {
        for (var c = 0x20; c <= 0x7E; c++)
        {
            var ch = (char)c;
            if (!char.IsLetterOrDigit(ch))
            {
                yield return ch.ToString();
            }
        }
    }

    private static IEnumerable<string> GetNonAsciiSymbols()
    {
        for (var codePoint = 0x80; codePoint <= 0x10FFFF; codePoint++)
        {
            if (!Rune.IsValid(codePoint))
            {
                continue;
            }

            var rune = new Rune(codePoint);
            var category = Rune.GetUnicodeCategory(rune);
            if (category is UnicodeCategory.MathSymbol or
                UnicodeCategory.CurrencySymbol or
                UnicodeCategory.ModifierSymbol or
                UnicodeCategory.OtherSymbol)
            {
                yield return rune.ToString();
            }
        }
    }

    private static IEnumerable<string> GetSingleCodePointEmoji()
    {
        foreach (var (start, end) in EmojiPresentationRanges)
        {
            for (var codePoint = start; codePoint <= end; codePoint++)
            {
                if (Rune.IsValid(codePoint))
                {
                    yield return new Rune(codePoint).ToString();
                }
            }
        }
    }

    private static ManagedGlyphKind ConvertKind(NativeGlyphKind nativeKind) =>
        nativeKind switch
        {
            NativeGlyphKind.Invalid => ManagedGlyphKind.Invalid,
            NativeGlyphKind.None => ManagedGlyphKind.None,
            NativeGlyphKind.Emoji => ManagedGlyphKind.Emoji,
            NativeGlyphKind.FluentSymbol => ManagedGlyphKind.FluentSymbol,
            NativeGlyphKind.Other => ManagedGlyphKind.Other,
            _ => throw new ArgumentOutOfRangeException(nameof(nativeKind), nativeKind, null),
        };

    private static string DescribeInput(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return "<empty>";
        }

        var codePoints = string.Join(
            " ",
            input.EnumerateRunes().Select(r => $"U+{r.Value:X4}"));

        return $"'{input}' ({codePoints})";
    }
}
