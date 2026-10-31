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
    public static string Expand(string text, Func<string, string?> prompt, Func<string?>? clipboard = null)
    {
        ArgumentNullException.ThrowIfNull(prompt);
        (string expanded, int cursor) = ExpandWithPromptRequests(text, request => prompt(request.Label), clipboard);
        return cursor < 0 ? expanded : expanded.Insert(cursor, CursorMarker);
    }

    /// <summary>
    /// Expands <paramref name="text"/>. <paramref name="prompt"/> is invoked for each {{prompt:Label}}, which is
    /// required, and {{prompt?:Label}}, which is optional, in order, and may return null (cancelled) to leave that
    /// spot blank. A null <paramref name="clipboard"/> or result expands {{clipboard}} to nothing.
    /// </summary>
    /// <returns>
    /// The text without the snippet's first cursor marker, and where that marker was, or -1. A "$|$" in an
    /// answer, the clipboard or a formatted date stays literal.
    /// </returns>
    public static (string Text, int Cursor) ExpandWithPromptRequests(string text, Func<PromptRequest, string?> prompt, Func<string?>? clipboard = null)
        => ExpandWithPromptRequests(text, prompt, clipboard, DateTime.Now, CultureInfo.CurrentCulture);

    /// <summary>Expands a snippet at <paramref name="now"/>, writing dates and times for <paramref name="culture"/>.</summary>
    internal static (string Text, int Cursor) ExpandWithPromptRequests(string text, Func<PromptRequest, string?> prompt, Func<string?>? clipboard, DateTime now, CultureInfo culture)
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

        var result = new StringBuilder(text.Length);
        int cursor = -1;
        bool clipboardRead = false;
        string? copied = null;
        for (int index = 0; index < tokens.Count; index++)
        {
            SnippetToken token = tokens[index];
            switch (token.Kind)
            {
                case SnippetTokenKind.Date:
                case SnippetTokenKind.Time:
                case SnippetTokenKind.Prompt:
                    result.Append(values[index]);
                    break;
                case SnippetTokenKind.Clipboard:
                    if (!clipboardRead)
                    {
                        copied = clipboard?.Invoke();
                        clipboardRead = true;
                    }

                    result.Append(copied);
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

    /// <summary>True when expanding this snippet requires showing UI.</summary>
    public static bool NeedsPrompt(string text)
        => text.Contains("{{prompt", StringComparison.Ordinal);

    /// <summary>True when expansion would incorporate user-supplied prompt or clipboard content.</summary>
    public static bool UsesSensitiveRuntimeInput(string text)
        => NeedsPrompt(text) || text.Contains("{{clipboard}}", StringComparison.Ordinal);
}
