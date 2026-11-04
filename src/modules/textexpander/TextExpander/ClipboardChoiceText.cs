// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text;

namespace Microsoft.PowerToys.TextExpander;

/// <summary>
/// What the window for a clipboard choice shows, apart from the window itself so it can be tested: the question, and each
/// match or copy on one line. The words come from the engine's resources (<see cref="EngineText"/>).
/// </summary>
internal static class ClipboardChoiceText
{
    /// <summary>The most characters a choice shows; its tooltip and its name for screen readers have more.</summary>
    internal const int MaxShown = 200;

    /// <summary>The most characters a choice's name for screen readers has, so a long copy isn't read for minutes.</summary>
    internal const int MaxSpoken = 1_000;

    /// <summary>The most characters a choice's tooltip has, so a huge copy doesn't lay out megabytes of text.</summary>
    internal const int MaxTip = 2_000;

    public static string InsertText => EngineText.Get("Choice_Insert", "Insert");

    /// <summary>Gets the question over the list, such as "Pick the link to insert", or "Pick a copy to insert" for whole copies.</summary>
    public static string Heading(ClipboardChoice choice) => choice.Part switch
    {
        ClipboardPart.Email => EngineText.Get("Choice_Heading_Email", "Pick the email address to insert"),
        ClipboardPart.Link => EngineText.Get("Choice_Heading_Link", "Pick the link to insert"),
        ClipboardPart.Phone => EngineText.Get("Choice_Heading_Phone", "Pick the phone number to insert"),
        ClipboardPart.Number => EngineText.Get("Choice_Heading_Number", "Pick the number to insert"),
        _ => EngineText.Get("Choice_Heading_Copy", "Pick a copy to insert"),
    };

    /// <summary>Gets at most <paramref name="max"/> characters of <paramref name="text"/>, then "…", not between the halves of a character.</summary>
    public static string Capped(string? text, int max)
    {
        text ??= string.Empty;
        if (text.Length <= max)
        {
            return text;
        }

        int end = char.IsHighSurrogate(text[max - 1]) ? max - 1 : max;
        return string.Concat(text.AsSpan(0, end), "\u2026");
    }

    /// <summary>
    /// Gets <paramref name="text"/> on one line, for the list: line breaks and tabs as spaces, spaces run together, and at
    /// most <see cref="MaxShown"/> characters, then "…".
    /// </summary>
    public static string OneLine(string? text)
    {
        var line = new StringBuilder(Math.Min(text?.Length ?? 0, MaxShown + 1));
        bool space = false;
        foreach (char character in text ?? string.Empty)
        {
            if (char.IsWhiteSpace(character) || char.IsControl(character))
            {
                space = line.Length > 0;
                continue;
            }

            if (line.Length + (space ? 1 : 0) >= MaxShown)
            {
                return line.Append('\u2026').ToString();
            }

            line.Append(space ? " " : string.Empty).Append(character);
            space = false;
        }

        return line.ToString();
    }
}
