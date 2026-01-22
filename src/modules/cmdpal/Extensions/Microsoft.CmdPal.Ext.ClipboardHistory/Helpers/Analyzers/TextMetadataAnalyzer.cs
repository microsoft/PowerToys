// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace Microsoft.CmdPal.Ext.ClipboardHistory.Helpers.Analyzers;

internal partial class TextMetadataAnalyzer
{
    public TextMetadata Analyze(string input)
    {
        ArgumentNullException.ThrowIfNull(input);

        return new TextMetadata
        {
            CharacterCount = input.Length,
            WordCount = CountWords(input),
            SentenceCount = CountSentences(input),
            LineCount = CountLines(input),
            ParagraphCount = CountParagraphs(input),
            LineEnding = DetectLineEnding(input),
        };
    }

    private LineEndingType DetectLineEnding(string text)
    {
        var crlfCount = Regex.Matches(text, "\r\n").Count;
        var lfCount = Regex.Matches(text, "(?<!\r)\n").Count;
        var crCount = Regex.Matches(text, "\r(?!\n)").Count;

        var endingTypes = (crlfCount > 0 ? 1 : 0) + (lfCount > 0 ? 1 : 0) + (crCount > 0 ? 1 : 0);

        if (endingTypes > 1)
        {
            return LineEndingType.Mixed;
        }

        if (crlfCount > 0)
        {
            return LineEndingType.Windows;
        }

        if (lfCount > 0)
        {
            return LineEndingType.Unix;
        }

        if (crCount > 0)
        {
            return LineEndingType.Mac;
        }

        return LineEndingType.None;
    }

    private int CountLines(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return 0;
        }

        return text.Count(c => c == '\n') + 1;
    }

    private int CountParagraphs(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return 0;
        }

        var paragraphs = ParagraphsRegex()
            .Split(text)
            .Count(static p => !string.IsNullOrWhiteSpace(p));

        return paragraphs > 0 ? paragraphs : 1;
    }

    private int CountWords(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return 0;
        }

        return Regex.Matches(text, @"\b\w+\b").Count;
    }

    private int CountSentences(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return 0;
        }

        var matches = SentencesRegex().Matches(text);
        return matches.Count > 0 ? matches.Count : (text.Trim().Length > 0 ? 1 : 0);
    }

    [GeneratedRegex(@"(\r?\n){2,}")]
    private static partial Regex ParagraphsRegex();

    [GeneratedRegex(@"[.!?]+(?=\s|$)")]
    private static partial Regex SentencesRegex();
}
