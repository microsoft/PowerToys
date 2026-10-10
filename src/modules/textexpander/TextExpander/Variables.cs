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
    [GeneratedRegex(@"\{\{prompt:([^}]*)\}\}")]
    private static partial Regex PromptPattern();

    [GeneratedRegex(@"\{\{date:([^}]*)\}\}")]
    private static partial Regex CustomDatePattern();

    /// <summary>
    /// Expands <paramref name="text"/>. <paramref name="prompt"/> is invoked for each
    /// {{prompt:Label}} occurrence and may return null when the user cancels.
    /// <paramref name="clipboard"/> supplies {{clipboard}}; when it is null or returns null the
    /// placeholder expands to nothing rather than being typed out literally.
    /// </summary>
    public static string Expand(string text, Func<string, string?> prompt, Func<string?>? clipboard = null)
    {
        if (text.Contains("{{", StringComparison.Ordinal))
        {
            DateTime now = DateTime.Now;
            var sb = new StringBuilder(text);
            sb.Replace("{{date}}", now.ToString("MM/dd/yy", CultureInfo.InvariantCulture));
            sb.Replace("{{time}}", now.ToString("hh:mm tt", CultureInfo.InvariantCulture));
            sb.Replace("{{datetime}}", now.ToString("MM/dd/yy hh:mm tt", CultureInfo.InvariantCulture));
            sb.Replace("{{isodate}}", now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
            text = sb.ToString();
            text = CustomDatePattern().Replace(text, match => FormatDate(now, match.Groups[1].Value));

            // Each occurrence prompts separately, so two identical placeholders ask twice.
            foreach (Match match in PromptPattern().Matches(text))
            {
                string label = match.Groups[1].Value;
                string placeholder = "{{prompt:" + label + "}}";
                string value = prompt(label) ?? string.Empty;

                int index = text.IndexOf(placeholder, StringComparison.Ordinal);
                if (index >= 0)
                {
                    text = string.Concat(text.AsSpan(0, index), value, text.AsSpan(index + placeholder.Length));
                }
            }
        }

        if (text.Contains('\\', StringComparison.Ordinal))
        {
            text = text.Replace("\\n", "\n", StringComparison.Ordinal)
                       .Replace("\\t", "\t", StringComparison.Ordinal);
        }

        // Last, so clipboard contents are inserted verbatim. Substituting earlier would let a
        // copied "\n" or "{{date}}" be re-read as markup and rewritten behind the user's back.
        // Read here rather than at injection time because injection itself takes the clipboard
        // over to paste; by then the user's contents are gone.
        if (text.Contains("{{clipboard}}", StringComparison.Ordinal))
        {
            text = text.Replace("{{clipboard}}", clipboard?.Invoke() ?? string.Empty, StringComparison.Ordinal);
        }

        return text;
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
        => text.Contains("{{prompt:", StringComparison.Ordinal);
}
