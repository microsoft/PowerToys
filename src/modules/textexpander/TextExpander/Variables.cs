// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Microsoft.PowerToys.TextExpander;

/// <summary>
/// Expands {{variables}} and backslash escapes. Prompt placeholders are evaluated once per occurrence rather than once per label.
/// </summary>
internal static partial class Variables
{
    /// <summary>Marks where the caret ends up after the snippet is inserted.</summary>
    public const string CursorMarker = "$|$";

    private const string ClipboardPlaceholder = "{{clipboard}}";

    /// <summary>
    /// Every value a snippet can hold, read in one pass from the left: {{date}}, {{time}},
    /// {{datetime}}, {{isodate}}, {{date:FORMAT}}, {{prompt:Label}} and {{prompt?:Label}}. The Settings preview
    /// (<c>TextExpanderSnippetPreview</c>) reads snippets with the same pattern.
    /// </summary>
    [GeneratedRegex(@"\{\{(?:(?<fixed>date|time|datetime|isodate)|date:(?<format>[^}]*)|prompt(?<optional>\?)?:(?<label>[^}]*))\}\}")]
    private static partial Regex ValuePattern();

    /// <summary>
    /// Expands <paramref name="text"/>, keeping the snippet's cursor marker in place. See
    /// <see cref="ExpandWithCursor"/>.
    /// </summary>
    public static string Expand(string text, Func<string, string?> prompt, Func<string?>? clipboard = null)
    {
        ArgumentNullException.ThrowIfNull(prompt);
        (string expanded, int cursor) = ExpandWithPromptRequests(text, request => prompt(request.Label), clipboard);
        return cursor < 0 ? expanded : expanded.Insert(cursor, CursorMarker);
    }

    /// <summary>
    /// Expands <paramref name="text"/>. <paramref name="prompt"/> is invoked for each
    /// {{prompt:Label}} occurrence, in order, and may return null when the user cancels, which
    /// leaves that spot blank. Answers are inserted exactly as typed.
    /// <paramref name="clipboard"/> supplies {{clipboard}}; when it is null or returns null the
    /// placeholder expands to nothing rather than being typed out literally.
    /// </summary>
    /// <returns>
    /// The text without the snippet's first cursor marker, and where that marker was, or -1.
    /// Only the snippet's own text can place the cursor: a "$|$" in an answer, the clipboard or a
    /// formatted date stays as typed.
    /// </returns>
    public static (string Text, int Cursor) ExpandWithCursor(string text, Func<string, string?> prompt, Func<string?>? clipboard = null)
    {
        ArgumentNullException.ThrowIfNull(prompt);
        return ExpandWithPromptRequests(text, request => prompt(request.Label), clipboard);
    }

    /// <summary>
    /// Expands a snippet while preserving whether each prompt requires a non-empty answer.
    /// {{prompt:Label}} is required; {{prompt?:Label}} is optional.
    /// </summary>
    public static (string Text, int Cursor) ExpandWithPromptRequests(string text, Func<PromptRequest, string?> prompt, Func<string?>? clipboard = null)
    {
        ArgumentNullException.ThrowIfNull(prompt);

        // The snippet's own text, with each value kept apart from it: dates and answers here, the
        // clipboard below. Only the snippet's text is read for codes: an answer such as "C:\temp"
        // must not gain a tab, a date format that prints "\n" must keep it, and a typed
        // "{{clipboard}}" must not paste what was copied. One pass, so a long snippet with many
        // values costs no more than reading it.
        var parts = new List<(string Text, bool IsValue)>();
        if (text.Contains("{{", StringComparison.Ordinal))
        {
            DateTime now = DateTime.Now;
            int last = 0;

            // Each question is asked as it's reached, so two identical ones ask twice.
            foreach (Match match in ValuePattern().Matches(text))
            {
                parts.Add((text[last..match.Index], false));
                parts.Add((ValueOf(match, now, prompt), true));
                last = match.Index + match.Length;
            }

            parts.Add((text[last..], false));
        }
        else
        {
            parts.Add((text, false));
        }

        var result = new StringBuilder(text.Length);
        int cursor = -1;
        string? copied = null;
        bool clipboardRead = false;
        foreach ((string part, bool isValue) in parts)
        {
            if (isValue)
            {
                result.Append(part);
                continue;
            }

            string literal = part;
            if (literal.Contains('\\', StringComparison.Ordinal))
            {
                literal = literal.Replace("\\n", "\n", StringComparison.Ordinal)
                                 .Replace("\\t", "\t", StringComparison.Ordinal);
            }

            // Last, so clipboard contents are inserted verbatim. Substituting earlier would let a
            // copied "\n" or "{{date}}" be re-read as markup and rewritten behind the user's back.
            // Read here rather than at injection time because injection itself takes the clipboard
            // over to paste; by then the user's contents are gone. Read once, however many times
            // the snippet uses it.
            int start = 0;
            while (true)
            {
                int at = literal.IndexOf(ClipboardPlaceholder, start, StringComparison.Ordinal);
                AppendSnippetText(result, at < 0 ? literal.AsSpan(start) : literal.AsSpan(start, at - start), ref cursor);
                if (at < 0)
                {
                    break;
                }

                if (!clipboardRead)
                {
                    copied = clipboard?.Invoke();
                    clipboardRead = true;
                }

                result.Append(copied);
                start = at + ClipboardPlaceholder.Length;
            }
        }

        return (result.ToString(), cursor);
    }

    /// <summary>The value one match of <see cref="ValuePattern"/> stands for.</summary>
    private static string ValueOf(Match match, DateTime now, Func<PromptRequest, string?> prompt)
    {
        if (match.Groups["label"].Success)
        {
            return prompt(new PromptRequest(match.Groups["label"].Value, Required: !match.Groups["optional"].Success)) ?? string.Empty;
        }

        if (match.Groups["format"].Success)
        {
            return FormatDate(now, match.Groups["format"].Value);
        }

        return match.Groups["fixed"].Value switch
        {
            "date" => now.ToString("MM/dd/yy", CultureInfo.InvariantCulture),
            "time" => now.ToString("hh:mm tt", CultureInfo.InvariantCulture),
            "datetime" => now.ToString("MM/dd/yy hh:mm tt", CultureInfo.InvariantCulture),
            _ => now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        };
    }

    /// <summary>Appends the snippet's own text, taking out its first cursor marker and noting where it was.</summary>
    private static void AppendSnippetText(StringBuilder result, ReadOnlySpan<char> text, ref int cursor)
    {
        int marker = cursor < 0 ? text.IndexOf(CursorMarker, StringComparison.Ordinal) : -1;
        if (marker < 0)
        {
            result.Append(text);
            return;
        }

        cursor = result.Length + marker;
        result.Append(text[..marker]).Append(text[(marker + CursorMarker.Length)..]);
    }

    /// <summary>
    /// Formats a date for {{date:FORMAT}}. Accepts .NET format strings ("MM/dd/yyyy") and also
    /// the strftime specifiers ("%m/%d/%Y"), because formats arrive both hand-written and copied
    /// straight out of a YAML snippet library by the migration.
    /// </summary>
    internal static string FormatDate(DateTime value, string format)
    {
        if (format.Length == 0)
        {
            return string.Empty;
        }

        // A '%' is unambiguous: it is not a .NET custom format specifier on its own, and every
        // strftime format has one. Anything else is handed to .NET, which is the only way to
        // reach formats strftime cannot spell, such as a bare "dddd".
        if (format.Contains('%', StringComparison.Ordinal))
        {
            return FormatStrftime(value, format);
        }

        try
        {
            // A one-character format string is read by .NET as a *standard* specifier, so a bare
            // "d" would be a whole short date rather than the day of the month, and "s" a whole
            // sortable timestamp. {{date:d}} plainly asks for a field, so say so explicitly.
            // This cannot collide with the strftime branch above, which has already claimed
            // everything containing a '%'.
            string resolved = format.Length == 1 ? "%" + format : format;
            return value.ToString(resolved, CultureInfo.InvariantCulture);
        }
        catch (FormatException)
        {
            // An unparseable format must not take the whole expansion down. Emitting the format
            // verbatim leaves the user something they can recognise and correct.
            return format;
        }
    }

    /// <summary>
    /// Expands strftime specifiers, formatting each one as it is read.
    ///
    /// Deliberately not translated into a .NET format string and handed to ToString: literal
    /// text between specifiers would then have to be escaped, and missing one turns "%Y sure"
    /// into a year followed by the seconds and the universal-time pattern instead of the word.
    /// </summary>
    internal static string FormatStrftime(DateTime value, string format)
    {
        var result = new StringBuilder(format.Length + 16);
        for (int index = 0; index < format.Length; index++)
        {
            if (format[index] != '%' || index + 1 >= format.Length)
            {
                result.Append(format[index]);
                continue;
            }

            char specifier = format[++index];
            result.Append(specifier switch
            {
                '%' => "%",
                'a' => value.ToString("ddd", CultureInfo.InvariantCulture),
                'A' => value.ToString("dddd", CultureInfo.InvariantCulture),
                'b' or 'h' => value.ToString("MMM", CultureInfo.InvariantCulture),
                'B' => value.ToString("MMMM", CultureInfo.InvariantCulture),
                'd' => value.ToString("dd", CultureInfo.InvariantCulture),
                'e' => value.Day.ToString(CultureInfo.InvariantCulture).PadLeft(2, ' '),
                'F' => value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                'H' => value.ToString("HH", CultureInfo.InvariantCulture),
                'I' => value.ToString("hh", CultureInfo.InvariantCulture),
                'j' => value.DayOfYear.ToString("000", CultureInfo.InvariantCulture),
                'm' => value.ToString("MM", CultureInfo.InvariantCulture),
                'M' => value.ToString("mm", CultureInfo.InvariantCulture),
                'p' => value.ToString("tt", CultureInfo.InvariantCulture),
                'R' => value.ToString("HH:mm", CultureInfo.InvariantCulture),
                'S' => value.ToString("ss", CultureInfo.InvariantCulture),
                'T' => value.ToString("HH:mm:ss", CultureInfo.InvariantCulture),
                'u' => ((((int)value.DayOfWeek + 6) % 7) + 1)
                    .ToString(CultureInfo.InvariantCulture),
                'w' => ((int)value.DayOfWeek).ToString(CultureInfo.InvariantCulture),
                'x' => value.ToString("MM/dd/yy", CultureInfo.InvariantCulture),
                'X' => value.ToString("HH:mm:ss", CultureInfo.InvariantCulture),
                'y' => value.ToString("yy", CultureInfo.InvariantCulture),
                'Y' => value.ToString("yyyy", CultureInfo.InvariantCulture),
                'n' => "\n",
                't' => "\t",
                _ => $"%{specifier}",
            });
        }

        return result.ToString();
    }

    /// <summary>True when expanding this snippet requires showing UI.</summary>
    public static bool NeedsPrompt(string text)
        => text.Contains("{{prompt", StringComparison.Ordinal);

    /// <summary>True when expansion would incorporate user-supplied prompt or clipboard content.</summary>
    public static bool UsesSensitiveRuntimeInput(string text)
        => NeedsPrompt(text) || text.Contains("{{clipboard}}", StringComparison.Ordinal);
}
