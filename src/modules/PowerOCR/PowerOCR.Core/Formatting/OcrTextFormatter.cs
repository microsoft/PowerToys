// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using PowerOCR.Core.Models;

namespace PowerOCR.Core.Formatting;

public static partial class OcrTextFormatter
{
    [GeneratedRegex(@"(^[\p{L}-[\p{Lo}]]|\p{Nd}$)|.{2,}")]
    private static partial Regex SpaceJoiningWordRegex();

    [GeneratedRegex(@"[ ]{2,}")]
    private static partial Regex RepeatedSpacesRegex();

    public static string FormatDocument(OcrDocument document, string languageTag)
    {
        bool useOcrLineText = UsesSpaces(languageTag);
        bool isRightToLeft = CultureInfo.GetCultureInfo(languageTag).TextInfo.IsRightToLeft;
        var lines = new List<string>(document.Lines.Count);

        foreach (OcrLineData line in document.Lines)
        {
            string text = useOcrLineText ? line.Text : JoinCjkAwareWords(line.Words);
            if (isRightToLeft)
            {
                text = string.Join(' ', text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Reverse());
            }

            lines.Add(text);
        }

        return string.Join(Environment.NewLine, lines).Trim();
    }

    public static string CollapseToSingleLine(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        string collapsed = text
            .Replace("\r\n", " ", StringComparison.Ordinal)
            .Replace('\n', ' ')
            .Replace('\r', ' ');
        return RepeatedSpacesRegex().Replace(collapsed, " ").Trim();
    }

    public static bool UsesSpaces(string languageTag)
        => !languageTag.StartsWith("zh", StringComparison.OrdinalIgnoreCase)
           && !languageTag.Equals("ja", StringComparison.OrdinalIgnoreCase);

    internal static string JoinCjkAwareWords(IReadOnlyList<OcrWordData> words)
    {
        var builder = new StringBuilder();
        bool previousUsesSpace = false;

        for (int i = 0; i < words.Count; i++)
        {
            string word = words[i].Text;
            bool currentUsesSpace = SpaceJoiningWordRegex().IsMatch(word);
            if (i > 0 && (currentUsesSpace || previousUsesSpace))
            {
                builder.Append(' ');
            }

            builder.Append(word);
            previousUsesSpace = currentUsesSpace;
        }

        return builder.ToString();
    }
}
