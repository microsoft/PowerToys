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
    public static string Expand(string text, Func<string, string?> prompt, Func<string?>? clipboard = null, Func<ClipboardChoice, string?>? choose = null, Func<ClipboardHistory>? history = null)
    {
        ArgumentNullException.ThrowIfNull(prompt);
        (string expanded, int cursor) = ExpandWithPromptRequests(text, request => prompt(request.Label), clipboard, choose, history);
        return cursor < 0 ? expanded : expanded.Insert(cursor, CursorMarker);
    }

    /// <summary>
    /// Expands <paramref name="text"/>. <paramref name="prompt"/> is invoked for each {{prompt:Label}}, which is
    /// required, and {{prompt?:Label}}, which is optional, in order, and may return null (cancelled) to leave that
    /// spot blank. After the questions, each clipboard variable takes from what <paramref name="clipboard"/> reads, or from
    /// the copies <paramref name="history"/> reads for one that picks from clipboard history; each is read at most once,
    /// and only if a variable uses it. One match or copy goes straight in. Two or more ask <paramref name="choose"/> which
    /// (see <see cref="ClipboardChoice"/>), in the snippet's order, which is the same each time for the same copies; a null
    /// answer cancels, and nothing more is asked. Without <paramref name="choose"/>, the first goes in. None inserts nothing,
    /// the rest still expands, and the result says why (see <see cref="ClipboardGap"/>).
    /// </summary>
    /// <returns>
    /// The text without the snippet's first cursor marker, where that marker was, or -1, and the spots left empty. A "$|$"
    /// in an answer, the clipboard or a formatted date stays literal.
    /// </returns>
    public static ExpandedSnippet ExpandWithPromptRequests(string text, Func<PromptRequest, string?> prompt, Func<string?>? clipboard = null, Func<ClipboardChoice, string?>? choose = null, Func<ClipboardHistory>? history = null)
        => ExpandWithPromptRequests(text, prompt, clipboard, DateTime.Now, CultureInfo.CurrentCulture, choose, history);

    /// <summary>Expands a snippet at <paramref name="now"/>, writing dates and times and reading numbers for <paramref name="culture"/>.</summary>
    internal static ExpandedSnippet ExpandWithPromptRequests(string text, Func<PromptRequest, string?> prompt, Func<string?>? clipboard, DateTime now, CultureInfo culture, Func<ClipboardChoice, string?>? choose = null, Func<ClipboardHistory>? history = null)
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

        IReadOnlyList<ClipboardGap> gaps = ResolveClipboard(tokens, values, clipboard, choose, history, culture);

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

        return new ExpandedSnippet(result.ToString(), cursor, gaps);
    }

    /// <summary>
    /// Works out what each clipboard variable inserts, from options gathered for all of them first, so the last window to
    /// show knows it's last: keys typed once it's answered are meant for after the snippet. A cancelled choice ends it.
    /// </summary>
    private static IReadOnlyList<ClipboardGap> ResolveClipboard(IReadOnlyList<SnippetToken> tokens, string?[] values, Func<string?>? clipboard, Func<ClipboardChoice, string?>? choose, Func<ClipboardHistory>? history, CultureInfo culture)
    {
        IReadOnlyList<string>?[]? options = null;
        var reasons = new ClipboardGapReason[tokens.Count];
        string? copied = null;
        bool readClipboard = false;
        ClipboardHistory? copies = null;
        for (int index = 0; index < tokens.Count; index++)
        {
            if (tokens[index].Kind != SnippetTokenKind.Clipboard)
            {
                continue;
            }

            options ??= new IReadOnlyList<string>?[tokens.Count];
            ClipboardVariable variable = tokens[index].Clipboard;
            if (variable.PickCopy)
            {
                copies ??= history?.Invoke() ?? ClipboardHistory.NotRead;
                (options[index], reasons[index]) = FromHistory(copies, variable.Part, culture);
                continue;
            }

            if (!readClipboard)
            {
                copied = clipboard?.Invoke();
                readClipboard = true;
            }

            IReadOnlyList<string> found = ClipboardParts.Find(copied, variable.Part, culture);
            options[index] = found.Count > ClipboardChoice.MaxOptions ? found.Take(ClipboardChoice.MaxOptions).ToList() : found;
            reasons[index] = ClipboardGapReason.NotFound;
        }

        if (options is null)
        {
            return Array.Empty<ClipboardGap>();
        }

        // asks[i]: whether a window may show for a variable at i or after it.
        var asks = new bool[tokens.Count + 1];
        for (int index = tokens.Count - 1; index >= 0; index--)
        {
            asks[index] = (choose is not null && options[index] is { Count: > 1 }) || asks[index + 1];
        }

        var gaps = new List<ClipboardGap>();
        for (int index = 0; index < tokens.Count; index++)
        {
            if (options[index] is not { } found)
            {
                continue;
            }

            ClipboardVariable variable = tokens[index].Clipboard;
            if (found.Count == 0)
            {
                gaps.Add(new ClipboardGap(variable.Part, reasons[index]));
                continue;
            }

            if (found.Count == 1 || choose is null)
            {
                values[index] = found[0];
                continue;
            }

            values[index] = choose(new ClipboardChoice(variable.Part, variable.PickCopy, found, IsLast: !asks[index + 1]));
            if (values[index] is null)
            {
                break;
            }
        }

        return gaps;
    }

    /// <summary>
    /// Gets what a variable that picks from clipboard history can insert: the copies themselves, or the different matches of
    /// its part across them, newest first. None when history is off, couldn't be read, has no copies or none has the part,
    /// and then which.
    /// </summary>
    private static (IReadOnlyList<string> Options, ClipboardGapReason Reason) FromHistory(ClipboardHistory history, ClipboardPart part, CultureInfo culture)
    {
        if (history.State != ClipboardHistoryState.Read)
        {
            return (Array.Empty<string>(), history.State == ClipboardHistoryState.Off ? ClipboardGapReason.HistoryOff : ClipboardGapReason.HistoryUnreadable);
        }

        // An email address is the same however it's capitalized, as within one copy.
        var seen = new HashSet<string>(part == ClipboardPart.Email ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
        var options = new List<string>();
        bool anyCopy = false;
        foreach (string copy in history.Copies)
        {
            if (string.IsNullOrEmpty(copy))
            {
                continue;
            }

            anyCopy = true;
            foreach (string found in ClipboardParts.Find(copy, part, culture))
            {
                if (seen.Add(found))
                {
                    options.Add(found);
                    if (options.Count == ClipboardChoice.MaxOptions)
                    {
                        return (options, ClipboardGapReason.NotFound);
                    }
                }
            }
        }

        return (options, anyCopy ? ClipboardGapReason.NotFound : ClipboardGapReason.NoCopies);
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
    /// True when a clipboard variable in this snippet may show a window to choose when it runs: a part, which may have two
    /// or more matches, or a pick from clipboard history.
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
