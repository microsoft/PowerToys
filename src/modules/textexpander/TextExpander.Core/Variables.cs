// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System.Globalization;
using System.Text;

namespace Microsoft.PowerToys.TextExpander;

/// <summary>
/// Expands {{variables}} and backslash escapes. Prompt placeholders are evaluated once per occurrence rather than once per label.
/// </summary>
public static partial class Variables
{
    /// <summary>Marks where the caret ends up after the snippet is inserted.</summary>
    public const string CursorMarker = "$|$";

    /// <summary>Expands <paramref name="text"/> like <see cref="ExpandWithCursor"/>, keeping the cursor marker in place.</summary>
    public static string Expand(string text, Func<string, string?> prompt, Func<string?>? clipboard = null)
    {
        ArgumentNullException.ThrowIfNull(prompt);
        (string expanded, int cursor) = ExpandWithPromptRequests(text, request => prompt(request.Label), clipboard);
        return cursor < 0 ? expanded : expanded.Insert(cursor, CursorMarker);
    }

    /// <summary>
    /// Expands <paramref name="text"/>. <paramref name="prompt"/> is invoked for each {{prompt:Label}}, in order,
    /// and may return null (cancelled) to leave that spot blank. A null <paramref name="clipboard"/> or result
    /// expands {{clipboard}} to nothing.
    /// </summary>
    /// <returns>
    /// The text without the snippet's first cursor marker, and where that marker was, or -1. A "$|$" in an
    /// answer, the clipboard or a formatted date stays literal.
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

        IReadOnlyList<SnippetToken> tokens = SnippetTemplate.Parse(text);
        DateTime now = DateTime.Now;
        var values = new string?[tokens.Count];
        for (int index = 0; index < tokens.Count; index++)
        {
            SnippetToken token = tokens[index];

            // Ask every question before reading the clipboard, preserving the existing expansion order.
            values[index] = token.Kind switch
            {
                SnippetTokenKind.Prompt => prompt(new PromptRequest(token.Value, token.Required)),
                SnippetTokenKind.Date or SnippetTokenKind.Time => FormatDate(now, token.Value),
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
    /// Formats a date for {{date:FORMAT}}, accepting .NET format strings ("MM/dd/yyyy") and strftime ("%m/%d/%Y").
    /// </summary>
    internal static string FormatDate(DateTime value, string format)
    {
        if (format.Length == 0)
        {
            return string.Empty;
        }

        // A '%' means strftime (it is not a .NET custom specifier on its own); anything else goes to .NET,
        // which also reaches formats strftime can't spell, such as "dddd".
        if (format.Contains('%', StringComparison.Ordinal))
        {
            return FormatStrftime(value, format);
        }

        try
        {
            // A one-character format is a .NET *standard* specifier ("d" is a short date), so {{date:d}} is made
            // a custom field explicitly. Strings containing '%' were already handled above.
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
    /// Expands strftime specifiers one by one, rather than translating to a .NET format string that would
    /// need every literal escaped.
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
