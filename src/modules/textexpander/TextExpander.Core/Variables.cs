// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System.Globalization;
using System.Text;

namespace Microsoft.PowerToys.TextExpander;

/// <summary>
/// Expands {{variables}}. Prompt placeholders are evaluated once per occurrence rather than once per label.
/// </summary>
public static partial class Variables
{
    /// <summary>Marks where the caret ends up after the snippet is inserted.</summary>
    public const string CursorMarker = "$|$";

    /// <summary>Expands <paramref name="text"/>, keeping the cursor marker in place.</summary>
    public static string Expand(string text, Func<string, string?> prompt, Func<string?>? clipboard = null, Func<ClipboardChoice, string?>? choose = null)
    {
        ArgumentNullException.ThrowIfNull(prompt);
        (string expanded, int cursor) = ExpandWithPromptRequests(text, request => prompt(request.Label), clipboard, choose);
        return cursor < 0 ? expanded : expanded.Insert(cursor, CursorMarker);
    }

    /// <summary>
    /// Expands <paramref name="text"/>. <paramref name="prompt"/> is invoked for each {{prompt:Label}}, which is
    /// required, and {{prompt?:Label}}, which is optional, in order, and may return null (cancelled) to leave that
    /// spot blank. A null <paramref name="clipboard"/> or result expands {{clipboard}} to nothing. After the questions,
    /// <paramref name="choose"/> is invoked for each choice a clipboard variable asks for (see <see cref="ClipboardChoice"/>);
    /// without it, a copy from clipboard history is nothing and a match to pick is the first.
    /// </summary>
    /// <returns>
    /// The text without the snippet's first cursor marker, and where that marker was, or -1. A "$|$" in an
    /// answer, the clipboard or a formatted date stays literal.
    /// </returns>
    public static (string Text, int Cursor) ExpandWithPromptRequests(string text, Func<PromptRequest, string?> prompt, Func<string?>? clipboard = null, Func<ClipboardChoice, string?>? choose = null)
        => ExpandWithPromptRequests(text, prompt, clipboard, DateTime.Now, CultureInfo.CurrentCulture, choose);

    /// <summary>Expands a snippet at <paramref name="now"/>, writing dates and times and reading numbers for <paramref name="culture"/>.</summary>
    internal static (string Text, int Cursor) ExpandWithPromptRequests(string text, Func<PromptRequest, string?> prompt, Func<string?>? clipboard, DateTime now, CultureInfo culture, Func<ClipboardChoice, string?>? choose = null)
    {
        ArgumentNullException.ThrowIfNull(prompt);

        IReadOnlyList<SnippetToken> tokens = SnippetTemplate.Parse(text);
        var values = new string?[tokens.Count];
        for (int index = 0; index < tokens.Count; index++)
        {
            SnippetToken token = tokens[index];

            // Ask every question before reading the clipboard, preserving the existing expansion order.
            values[index] = token.Kind switch
            {
                SnippetTokenKind.Prompt => prompt(new PromptRequest(token.Value, token.Required)),
                SnippetTokenKind.Date or SnippetTokenKind.Time => FormatToken(now, token, culture),
                _ => null,
            };
        }

        ResolveClipboard(tokens, values, clipboard, choose, culture);

        var result = new StringBuilder(text.Length);
        int cursor = -1;
        for (int index = 0; index < tokens.Count; index++)
        {
            SnippetToken token = tokens[index];
            switch (token.Kind)
            {
                case SnippetTokenKind.Date:
                case SnippetTokenKind.Time:
                case SnippetTokenKind.Prompt:
                case SnippetTokenKind.Clipboard:
                    result.Append(values[index]);
                    break;
                case SnippetTokenKind.Cursor:
                    cursor = result.Length;
                    break;
                case SnippetTokenKind.LineBreak:
                    result.Append('\n');
                    break;
                case SnippetTokenKind.Tab:
                    result.Append('\t');
                    break;
                default:
                    result.Append(token.Value);
                    break;
            }
        }

        return (result.ToString(), cursor);
    }

    /// <summary>
    /// Works out what each clipboard variable inserts. What's on the clipboard now is read once, and only if a variable
    /// uses it. A part that isn't there inserts nothing. A window asks for a copy from clipboard history, and for a match
    /// when there are two or more to pick from; the last window to show says so, since keys typed once it's answered are
    /// meant for after the snippet.
    /// </summary>
    private static void ResolveClipboard(IReadOnlyList<SnippetToken> tokens, string?[] values, Func<string?>? clipboard, Func<ClipboardChoice, string?>? choose, CultureInfo culture)
    {
        int first = 0;
        while (first < tokens.Count && tokens[first].Kind != SnippetTokenKind.Clipboard)
        {
            first++;
        }

        if (first == tokens.Count)
        {
            return;
        }

        string? copied = null;
        bool read = false;
        var found = new IReadOnlyList<string>?[tokens.Count];
        var asks = new bool[tokens.Count + 1];
        for (int index = first; index < tokens.Count; index++)
        {
            if (tokens[index].Kind != SnippetTokenKind.Clipboard || tokens[index].Clipboard.PickCopy)
            {
                continue;
            }

            if (!read)
            {
                copied = clipboard?.Invoke();
                read = true;
            }

            ClipboardVariable variable = tokens[index].Clipboard;
            if (variable.Part != ClipboardPart.All)
            {
                found[index] = ClipboardParts.Find(copied, variable.Part, culture);
            }
        }

        // asks[i]: whether a window may show for a variable at i or after it.
        for (int index = tokens.Count - 1; index >= 0; index--)
        {
            SnippetToken token = tokens[index];
            bool shows = token.Kind == SnippetTokenKind.Clipboard
                && (token.Clipboard.PickCopy || (token.Clipboard.PicksMatch && found[index] is { Count: > 1 }));
            asks[index] = shows || asks[index + 1];
        }

        for (int index = 0; index < tokens.Count; index++)
        {
            if (tokens[index].Kind != SnippetTokenKind.Clipboard)
            {
                continue;
            }

            ClipboardVariable variable = tokens[index].Clipboard;
            bool laterAsks = asks[index + 1];
            string? copy = copied;
            IReadOnlyList<string>? matches = found[index];
            if (variable.PickCopy)
            {
                // A copy with a match to pick may ask again, once its matches are known.
                copy = choose?.Invoke(new ClipboardChoice(ClipboardChoiceKind.Copy, variable.Part, Array.Empty<string>(), IsLast: !laterAsks && !variable.PicksMatch))
                    ?? (choose is null ? string.Empty : null);
                matches = variable.Part == ClipboardPart.All || copy is null ? null : ClipboardParts.Find(copy, variable.Part, culture);
            }

            values[index] = variable.Part == ClipboardPart.All ? copy : Choose(matches, variable, choose, isLast: !laterAsks);
        }
    }

    /// <summary>Gets the match <paramref name="variable"/> inserts, or null when there's none.</summary>
    private static string? Choose(IReadOnlyList<string>? matches, ClipboardVariable variable, Func<ClipboardChoice, string?>? choose, bool isLast)
    {
        if (matches is not { Count: > 0 })
        {
            return null;
        }

        return variable.Match switch
        {
            ClipboardMatch.Numbered => variable.Index <= matches.Count ? matches[variable.Index - 1] : null,
            ClipboardMatch.Last => matches[^1],
            ClipboardMatch.All => string.Join('\n', matches),
            ClipboardMatch.Pick when matches.Count > 1 && choose is not null => choose(new ClipboardChoice(ClipboardChoiceKind.Match, variable.Part, matches, isLast)),
            _ => matches[0],
        };
    }

    /// <summary>
    /// Formats a date or time token: {{date}}, {{time}} and {{datetime}} in <paramref name="culture"/>'s short
    /// formats, {{isodate}} as yyyy-MM-dd, and {{date:FORMAT}} by <see cref="FormatDate(DateTime, string, CultureInfo)"/>.
    /// </summary>
    internal static string FormatToken(DateTime value, SnippetToken token, CultureInfo culture) => !token.Fixed
        ? FormatDate(value, token.Value, culture)
        : token.Value switch
        {
            "date" => value.ToString("d", culture),
            "time" => value.ToString("t", culture),
            "datetime" => value.ToString("g", culture),
            _ => value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        };

    /// <summary>
    /// Formats a date for {{date:FORMAT}}, accepting .NET format strings ("MM/dd/yyyy") and strftime ("%m/%d/%Y").
    /// Names, AM and PM, and the calendar come from <paramref name="culture"/>; a "/" or ":" stays as typed
    /// rather than becoming the culture's separator.
    /// </summary>
    internal static string FormatDate(DateTime value, string format, CultureInfo culture)
    {
        if (format.Length == 0)
        {
            return string.Empty;
        }

        var typed = (DateTimeFormatInfo)culture.DateTimeFormat.Clone();
        typed.DateSeparator = "/";
        typed.TimeSeparator = ":";

        // A '%' means strftime (it is not a .NET custom specifier on its own); anything else goes to .NET,
        // which also reaches formats strftime can't spell, such as "dddd".
        if (format.Contains('%', StringComparison.Ordinal))
        {
            return FormatStrftime(value, format, culture, typed);
        }

        try
        {
            // A one-character format is a .NET *standard* specifier ("d" is a short date), so {{date:d}} is made
            // a custom field explicitly. Strings containing '%' were already handled above.
            string resolved = format.Length == 1 ? "%" + format : format;
            return value.ToString(resolved, typed);
        }
        catch (FormatException)
        {
            // An unparseable format must not take the whole expansion down. Emitting the format
            // verbatim leaves the user something they can recognise and correct.
            return format;
        }
    }

    /// <summary>
    /// Expands strftime specifiers one by one, rather than translating to a .NET format string that would
    /// need every literal escaped. %x and %X are <paramref name="culture"/>'s short date and long time, and %F
    /// is always yyyy-MM-dd.
    /// </summary>
    private static string FormatStrftime(DateTime value, string format, CultureInfo culture, DateTimeFormatInfo typed)
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
                'a' => value.ToString("ddd", typed),
                'A' => value.ToString("dddd", typed),
                'b' or 'h' => value.ToString("MMM", typed),
                'B' => value.ToString("MMMM", typed),
                'd' => value.ToString("dd", typed),
                'e' => value.ToString("%d", typed).PadLeft(2, ' '),
                'F' => value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                'H' => value.ToString("HH", typed),
                'I' => value.ToString("hh", typed),
                'j' => typed.Calendar.GetDayOfYear(value).ToString("000", CultureInfo.InvariantCulture),
                'm' => value.ToString("MM", typed),
                'M' => value.ToString("mm", typed),
                'p' => value.ToString("tt", typed),
                'R' => value.ToString("HH:mm", typed),
                'S' => value.ToString("ss", typed),
                'T' => value.ToString("HH:mm:ss", typed),
                'u' => ((((int)value.DayOfWeek + 6) % 7) + 1)
                    .ToString(CultureInfo.InvariantCulture),
                'w' => ((int)value.DayOfWeek).ToString(CultureInfo.InvariantCulture),
                'x' => value.ToString("d", culture),
                'X' => value.ToString("T", culture),
                'y' => value.ToString("yy", typed),
                'Y' => value.ToString("yyyy", typed),
                'n' => "\n",
                't' => "\t",
                _ => $"%{specifier}",
            });
        }

        return result.ToString();
    }

    /// <summary>True when expanding this snippet asks a question.</summary>
    public static bool NeedsPrompt(string text)
        => text.Contains("{{prompt", StringComparison.Ordinal);

    /// <summary>
    /// True when a clipboard variable in this snippet may show a window to choose something when it runs: a copy from
    /// clipboard history, or one of several matches.
    /// </summary>
    public static bool MayChoose(string text)
        => text.Contains("{{clipboard:", StringComparison.Ordinal)
            && SnippetTemplate.Parse(text).Any(token => token.Kind == SnippetTokenKind.Clipboard && token.Clipboard.MayAsk);

    /// <summary>True when expanding this snippet may show a window: a question, or a choice from the clipboard.</summary>
    public static bool MayAsk(string text) => NeedsPrompt(text) || MayChoose(text);

    /// <summary>
    /// True when expansion would incorporate user-supplied prompt or clipboard content: any clipboard variable, and any
    /// text that only looks like one, which costs nothing but not being kept for undo.
    /// </summary>
    public static bool UsesSensitiveRuntimeInput(string text)
        => NeedsPrompt(text) || text.Contains("{{clipboard", StringComparison.Ordinal);
}
