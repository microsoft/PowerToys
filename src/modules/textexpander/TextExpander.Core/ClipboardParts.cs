// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System.Globalization;
using System.Text.RegularExpressions;

namespace Microsoft.PowerToys.TextExpander;

/// <summary>
/// Finds the parts a clipboard variable can insert from a copy: email addresses, web links, phone numbers and numbers,
/// each different one once, in order. It reads in linear time, so a huge copy can't stall an expansion, and only the first
/// <see cref="MaxScannedLength"/> characters. Numbers follow the region's decimal and group separators, and so does telling
/// a phone number from a number written in groups. Dates, times and IP addresses are neither, and nothing in a link or an
/// email address is a phone number or a number.
/// </summary>
public static class ClipboardParts
{
    /// <summary>How much of a copy is read for its parts.</summary>
    public const int MaxScannedLength = 1_000_000;

    // A phone number has 7 to 15 digits: 555-0123 at the least, and at most what the international format allows.
    private const int MinPhoneDigits = 7;
    private const int MaxPhoneDigits = 15;

    // Without backtracking, so text built to make a pattern work hard still takes linear time. A control character ends a
    // link, so one hidden in a copied link, such as Ctrl+O, isn't taken along.
    private static readonly Regex LinkPattern = new(
        @"(?:https?://|www\.)[^\s<>""\p{Cc}]+",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);

    private static readonly Regex EmailPattern = new(
        @"[A-Za-z0-9._%+\-]+@[A-Za-z0-9\-]+(?:\.[A-Za-z0-9\-]+)*\.[A-Za-z]{2,}",
        RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);

    // IP addresses, dates written with digits and a year of two or four digits (2026-11-02, 11/2/2026, 02.11.26, 02-11-26)
    // and times (10:30, 10:30am, 9:45PM, 10:30h), which are neither phone numbers nor numbers. IP addresses first, so
    // 192.168.1.10 isn't read as a date. Each part of one is up to 255, so 1.234.567.890, a number where dots group
    // digits, isn't one. A time also takes the character after it, which isn't a digit or a colon, as there's no
    // look-ahead without backtracking.
    private static readonly Regex DateTimePattern = new(
        @"\b(?:(?:(?:25[0-5]|2[0-4]\d|1\d\d|[1-9]?\d)(?:\.(?:25[0-5]|2[0-4]\d|1\d\d|[1-9]?\d)){3}|\d{4}-\d{1,2}-\d{1,2}(?:[T ]\d{1,2}:\d{2}(?::\d{2}(?:\.\d+)?)?(?:Z|[+\-]\d{2}:?\d{2})?)?|\d{4}/\d{1,2}/\d{1,2}|\d{4}\.\d{1,2}\.\d{1,2}|\d{1,2}/\d{1,2}/(?:\d{4}|\d{2})|\d{1,2}\.\d{1,2}\.(?:\d{4}|\d{2})|\d{1,2}-\d{1,2}-(?:\d{4}|\d{2}))\b|\d{1,2}:\d{2}(?::\d{2})?(?:[^\d:]|$))",
        RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);

    /// <summary>
    /// Gets the different matches of <paramref name="part"/> in <paramref name="text"/>, in order, read with
    /// <paramref name="culture"/>'s number separators. The whole text, for <see cref="ClipboardPart.All"/>.
    /// </summary>
    public static IReadOnlyList<string> Find(string? text, ClipboardPart part, CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(culture);
        if (string.IsNullOrEmpty(text))
        {
            return Array.Empty<string>();
        }

        if (part == ClipboardPart.All)
        {
            return [text];
        }

        if (text.Length > MaxScannedLength)
        {
            // Not between the halves of a character.
            text = text[..(char.IsHighSurrogate(text[MaxScannedLength - 1]) ? MaxScannedLength - 1 : MaxScannedLength)];
        }

        List<(int Start, int End)> spans;
        if (part == ClipboardPart.Link)
        {
            spans = FindLinks(text);
        }
        else if (part == ClipboardPart.Email)
        {
            spans = FindEmails(text);
        }
        else
        {
            List<(int Start, int End)> notNumbers = [.. FindLinks(text), .. FindEmails(text), .. Matches(DateTimePattern, text)];
            List<(int Start, int End)> phones = FindPhones(text, culture, new Mask(notNumbers));
            spans = part == ClipboardPart.Phone ? phones : FindNumbers(text, culture, new Mask([.. notNumbers, .. phones]));
        }

        // An email address is the same however it's capitalized; a link's path and anything else may not be. Each different
        // match is copied out once, however often the copy repeats it.
        var seen = new HashSet<string>(part == ClipboardPart.Email ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
        HashSet<string>.AlternateLookup<ReadOnlySpan<char>> seenSpans = seen.GetAlternateLookup<ReadOnlySpan<char>>();
        var found = new List<string>();
        foreach ((int start, int end) in spans)
        {
            ReadOnlySpan<char> match = text.AsSpan(start, end - start);
            if (!seenSpans.Contains(match))
            {
                string value = match.Length == text.Length ? text : match.ToString();
                seen.Add(value);
                found.Add(value);
            }
        }

        return found;
    }

    private static List<(int Start, int End)> Matches(Regex pattern, string text)
    {
        var spans = new List<(int Start, int End)>();
        foreach (ValueMatch match in pattern.EnumerateMatches(text))
        {
            spans.Add((match.Index, match.Index + match.Length));
        }

        return spans;
    }

    private static List<(int Start, int End)> FindLinks(string text)
    {
        var spans = new List<(int Start, int End)>();
        foreach ((int start, int matchEnd) in Matches(LinkPattern, text))
        {
            int end = TrimLink(text, start, matchEnd);
            int prefix = text[start] is 'w' or 'W' ? 4 : text.IndexOf("//", start, StringComparison.Ordinal) + 2 - start;
            if (end - start > prefix)
            {
                spans.Add((start, end));
            }
        }

        return spans;
    }

    /// <summary>
    /// Gets where a link ends without what ends the sentence around it, and without closing brackets it didn't open, as
    /// when it's written in parentheses. Brackets are counted once, so a link of thousands of them is trimmed in linear time.
    /// </summary>
    private static int TrimLink(string text, int start, int end)
    {
        // Counted by kind: (), [], {} and full-width parentheses.
        Span<int> opened = stackalloc int[4];
        Span<int> closed = stackalloc int[4];
        for (int index = start; index < end; index++)
        {
            char character = text[index];
            int kind = BracketKind(character);
            if (kind < 0)
            {
                continue;
            }

            if (Opening(character) != '\0')
            {
                closed[kind]++;
            }
            else
            {
                opened[kind]++;
            }
        }

        while (end > start)
        {
            char last = text[end - 1];
            int kind = Opening(last) != '\0' ? BracketKind(last) : -1;
            if (".,;:!?'\"*_~\u3001\u3002\uFF0C\uFF0E".Contains(last, StringComparison.Ordinal))
            {
                end--;
            }
            else if (kind >= 0 && closed[kind] > opened[kind])
            {
                closed[kind]--;
                end--;
            }
            else
            {
                break;
            }
        }

        return end;
    }

    private static int BracketKind(char bracket) => bracket switch
    {
        '(' or ')' => 0,
        '[' or ']' => 1,
        '{' or '}' => 2,
        '\uFF08' or '\uFF09' => 3,
        _ => -1,
    };

    private static char Opening(char closing) => closing switch
    {
        ')' => '(',
        ']' => '[',
        '}' => '{',
        '\uFF09' => '\uFF08',
        _ => '\0',
    };

    private static List<(int Start, int End)> FindEmails(string text)
    {
        var spans = new List<(int Start, int End)>();
        foreach ((int matchStart, int end) in Matches(EmailPattern, text))
        {
            // Dots ending a sentence before it aren't part of its name.
            int start = matchStart;
            while (text[start] == '.')
            {
                start++;
            }

            if (text[start] != '@')
            {
                spans.Add((start, end));
            }
        }

        return spans;
    }

    /// <summary>
    /// Finds phone numbers: 7 to 15 digits written in groups with spaces, dashes, dots, slashes or brackets between them,
    /// or all together after a +, such as +1 425 555 0123, +1(425)555-0123, (425) 555-0123, 555-0123, +44 (0)20 7946 0018
    /// or 01 23 45 67 89. Digits all together are a number, unless a + leads them. So are digits grouped as the region
    /// groups thousands, such as 1 234 567 in France, and two groups with a dot or comma between, such as 3.14159265.
    /// </summary>
    private static List<(int Start, int End)> FindPhones(string text, CultureInfo culture, Mask mask)
    {
        NumberFormat format = NumberFormat.Of(culture);
        var spans = new List<(int Start, int End)>();
        var groups = new List<(int Start, int End)>();
        var separators = new List<char>();
        int index = 0;
        while (index < text.Length)
        {
            if (mask.EndOfSpanAt(index) is int maskEnd and >= 0)
            {
                index = maskEnd;
                continue;
            }

            if (!StartsPhone(text, index) || (index > 0 && !CanStartPhoneAfter(text[index - 1])))
            {
                index++;
                continue;
            }

            groups.Clear();
            separators.Clear();
            bool plus = text[index] == '+';
            bool brackets = false;
            int digits = 0;
            int end = index;
            int position = plus ? index + 1 : index;
            while (position < text.Length && !mask.Covers(position))
            {
                bool closedBracket = false;
                if (text[position] == '(' && !brackets && digits <= 4)
                {
                    // An area code, a trunk 0 as in +44 (0)20, or a country code with its + as in (+44).
                    int open = position + 1;
                    if (open < text.Length && text[open] == '+' && digits == 0 && !plus)
                    {
                        plus = true;
                        open++;
                    }

                    int close = open;
                    while (close < text.Length && char.IsDigit(text[close]))
                    {
                        close++;
                    }

                    if (close == open || close - open > 5 || close >= text.Length || text[close] != ')')
                    {
                        break;
                    }

                    groups.Add((open, close));
                    digits += close - open;
                    brackets = true;
                    closedBracket = true;
                    position = close + 1;
                }
                else if (char.IsDigit(text[position]))
                {
                    int run = position;
                    while (run < text.Length && char.IsDigit(text[run]) && !mask.Covers(run))
                    {
                        run++;
                    }

                    groups.Add((position, run));
                    digits += run - position;
                    position = run;
                }
                else
                {
                    break;
                }

                end = position;
                if (digits > MaxPhoneDigits)
                {
                    break;
                }

                // A group may follow brackets directly, as in (425)555-0123, and brackets a group, as in +1(425).
                if (position < text.Length && (closedBracket ? char.IsDigit(text[position]) : text[position] == '(' && !brackets))
                {
                    continue;
                }

                // One separator, then another group.
                if (position + 1 < text.Length && IsPhoneSeparator(text[position])
                    && (char.IsDigit(text[position + 1]) || text[position + 1] == '(') && !mask.Covers(position + 1))
                {
                    separators.Add(text[position]);
                    position++;
                    continue;
                }

                break;
            }

            // A separator before brackets that weren't a group isn't between groups.
            while (separators.Count > 0 && separators.Count >= groups.Count)
            {
                separators.RemoveAt(separators.Count - 1);
            }

            bool isPhone = digits >= MinPhoneDigits && digits <= MaxPhoneDigits
                && (plus || brackets || separators.Count > 0)
                && (end >= text.Length || !char.IsLetterOrDigit(text[end]))
                && (plus || brackets || (!IsDate(groups, separators) && !IsGroupedNumber(groups, separators, format)
                    && !(groups.Count == 2 && separators[0] is '.' or ',')));
            if (isPhone)
            {
                spans.Add((index, end));
                index = end;
            }
            else if (digits > MaxPhoneDigits)
            {
                // Too long to be one, so the rest of its run isn't one either, as with a long reference's last groups.
                index = SkipRun(text, position);
            }
            else
            {
                // The digits looked at aren't looked at again, so a date's parts don't start numbers of their own.
                index = Math.Max(position, index + 1);
            }
        }

        return spans;
    }

    /// <summary>Gets whether a phone number can start at <paramref name="index"/>: a digit, a + before one, or a bracket before either.</summary>
    private static bool StartsPhone(string text, int index)
    {
        char character = text[index];
        if (char.IsDigit(character))
        {
            return true;
        }

        if (index + 1 >= text.Length || character is not ('+' or '('))
        {
            return false;
        }

        char next = text[index + 1];
        return char.IsDigit(next) || (character == '(' && next == '+' && index + 2 < text.Length && char.IsDigit(text[index + 2]));
    }

    /// <summary>Gets where the run of digits, and brackets and separators between them, that <paramref name="position"/> is in ends.</summary>
    private static int SkipRun(string text, int position)
    {
        while (position < text.Length)
        {
            char character = text[position];
            bool joins = char.IsDigit(character) || character is '(' or ')'
                || (IsPhoneSeparator(character) && position + 1 < text.Length && (char.IsDigit(text[position + 1]) || text[position + 1] == '('));
            if (!joins)
            {
                break;
            }

            position++;
        }

        return position;
    }

    private static bool CanStartPhoneAfter(char previous)
        => !char.IsLetterOrDigit(previous) && previous is not ('_' or '@' or '/' or '.' or ',' or '+' or '-');

    private static bool IsPhoneSeparator(char character)
        => character is ' ' or '\u00A0' or '\u202F' or '-' or '\u2010' or '\u2011' or '.' or '/';

    /// <summary>Gets a value indicating whether three groups with one separator are a date, such as 2026-11-02 or 02.11.2026.</summary>
    private static bool IsDate(List<(int Start, int End)> groups, List<char> separators)
    {
        if (groups.Count != 3 || separators.Count != 2 || separators[0] != separators[1] || separators[0] is not ('/' or '.' or '-'))
        {
            return false;
        }

        int first = groups[0].End - groups[0].Start;
        int second = groups[1].End - groups[1].Start;
        int third = groups[2].End - groups[2].Start;
        return second <= 2 && ((first == 4 && third <= 2) || (first <= 2 && third is 2 or 4));
    }

    /// <summary>
    /// Gets a value indicating whether the groups are a number written in groups, such as 1,234,567, 1.234.567 or, where
    /// the region groups with spaces, 1 234 567: one separator throughout, and groups of the region's sizes or of three.
    /// </summary>
    private static bool IsGroupedNumber(List<(int Start, int End)> groups, List<char> separators, NumberFormat format)
    {
        if (separators.Count == 0 || separators.Exists(separator => separator != separators[0]))
        {
            return false;
        }

        char separator = separators[0];
        bool regionGroups = format.IsGroupSeparator(separator);
        if (!regionGroups && separator is not ('.' or ',' or '\''))
        {
            return false;
        }

        int[] sizes = regionGroups ? format.GroupSizes : [3];
        return HasGrouping(groups, groups.Count, sizes);
    }

    /// <summary>
    /// Finds numbers: digits, with a minus sign in front and the region's decimal separator and group separators between
    /// them, or a dot or comma as the decimal separator. Digits after a letter, as in ID123, aren't one. Digits that are
    /// none of those, such as 1.2.3, are a number each.
    /// </summary>
    private static List<(int Start, int End)> FindNumbers(string text, CultureInfo culture, Mask mask)
    {
        NumberFormat format = NumberFormat.Of(culture);
        var spans = new List<(int Start, int End)>();
        var groups = new List<(int Start, int End)>();
        var separators = new List<char>();
        int index = 0;
        while (index < text.Length)
        {
            if (mask.EndOfSpanAt(index) is int maskEnd and >= 0)
            {
                index = maskEnd;
                continue;
            }

            char character = text[index];
            bool sign = character is '-' or '\u2212' && index + 1 < text.Length && char.IsDigit(text[index + 1]) && !mask.Covers(index + 1);
            if (!(sign || char.IsDigit(character)) || (index > 0 && !CanStartNumberAfter(text, index, format)))
            {
                index++;
                continue;
            }

            groups.Clear();
            separators.Clear();
            int position = sign ? index + 1 : index;
            while (true)
            {
                int run = position;
                while (run < text.Length && char.IsDigit(text[run]) && !mask.Covers(run))
                {
                    run++;
                }

                groups.Add((position, run));
                position = run;
                if (position + 1 < text.Length && format.IsSeparator(text[position]) && char.IsDigit(text[position + 1]) && !mask.Covers(position + 1))
                {
                    separators.Add(text[position]);
                    position++;
                    continue;
                }

                break;
            }

            // The region's way, then a dot or a comma as the decimal separator with the other between groups, so a number
            // written elsewhere's way is one too, wherever the region puts its decimal separator.
            if (IsNumber(groups, separators, format.DecimalSeparator, format, format.GroupSizes)
                || (separators.Count == 1 && separators[0] is '.' or ',')
                || IsNumber(groups, separators, '.', other: null, [3])
                || IsNumber(groups, separators, ',', other: null, [3]))
            {
                spans.Add((index, position));
            }
            else
            {
                // Not one number, as in a version like 1.2.3: each group is one, the first with the sign.
                for (int group = 0; group < groups.Count; group++)
                {
                    spans.Add((group == 0 ? index : groups[group].Start, groups[group].End));
                }
            }

            index = position;
        }

        return spans;
    }

    /// <summary>A number starts after anything but a letter or digit, and not after a separator that follows a digit.</summary>
    private static bool CanStartNumberAfter(string text, int index, NumberFormat format)
    {
        char previous = text[index - 1];
        return !char.IsLetterOrDigit(previous) && previous != '_'
            && !(format.IsSeparator(previous) && index >= 2 && char.IsDigit(text[index - 2]));
    }

    /// <summary>
    /// Gets a value indicating whether the groups are one number with <paramref name="decimalSeparator"/> before the last
    /// group, if any, and group separators that <paramref name="other"/> accepts (a comma or dot, the one that isn't the
    /// decimal separator, when null) between groups of <paramref name="sizes"/>.
    /// </summary>
    private static bool IsNumber(List<(int Start, int End)> groups, List<char> separators, char decimalSeparator, NumberFormat? other, int[] sizes)
    {
        if (separators.Count == 0)
        {
            return true;
        }

        bool IsGroup(char separator) => other?.IsGroupSeparator(separator) ?? (separator is '.' or ',' && separator != decimalSeparator);

        int whole = separators[^1] == decimalSeparator ? groups.Count - 1 : groups.Count;
        for (int index = 0; index < whole - 1; index++)
        {
            if (!IsGroup(separators[index]))
            {
                return false;
            }
        }

        return whole == 1 || HasGrouping(groups, whole, sizes);
    }

    /// <summary>
    /// Gets a value indicating whether the first <paramref name="count"/> groups are grouped as <paramref name="sizes"/>
    /// say, from the right: the last group the first size, each before it the next, the last size repeating, and the first
    /// group at most its size.
    /// </summary>
    private static bool HasGrouping(List<(int Start, int End)> groups, int count, int[] sizes)
    {
        if (count < 2 || sizes.Length == 0)
        {
            return false;
        }

        for (int index = count - 1, size = 0; index >= 0; index--, size = Math.Min(size + 1, sizes.Length - 1))
        {
            int length = groups[index].End - groups[index].Start;
            int expected = sizes[size];
            if (expected <= 0 || (index > 0 ? length != expected : length > expected))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// The region's number separators: its decimal separator, and its group separator and sizes. Where it groups with a
    /// space, any space between digits groups, since people type an ordinary one; an apostrophe groups where a typographic
    /// one does.
    /// </summary>
    private sealed class NumberFormat
    {
        private readonly char _group;
        private readonly bool _spaceGroups;

        private NumberFormat(NumberFormatInfo info)
        {
            DecimalSeparator = info.NumberDecimalSeparator is { Length: 1 } decimalSeparator ? decimalSeparator[0] : '.';
            _group = info.NumberGroupSeparator is { Length: 1 } group ? group[0] : '\0';
            _spaceGroups = char.IsWhiteSpace(_group);
            GroupSizes = info.NumberGroupSizes.Length > 0 ? info.NumberGroupSizes : [3];
        }

        public char DecimalSeparator { get; }

        public int[] GroupSizes { get; }

        public static NumberFormat Of(CultureInfo culture) => new(culture.NumberFormat);

        public bool IsGroupSeparator(char character)
            => character != DecimalSeparator
                && (character == _group
                    || (_spaceGroups && character is ' ' or '\u00A0' or '\u202F' or '\u2009')
                    || (_group is '\'' or '\u2019' && character is '\'' or '\u2019'));

        /// <summary>Gets a value indicating whether <paramref name="character"/> can be between a number's digits.</summary>
        public bool IsSeparator(char character)
            => character is '.' or ',' || character == DecimalSeparator || IsGroupSeparator(character);
    }

    /// <summary>Spans of text that a part can't be in, such as links for numbers, looked up by binary search.</summary>
    private sealed class Mask
    {
        private readonly List<(int Start, int End)> _spans = new();

        public Mask(List<(int Start, int End)> spans)
        {
            spans.Sort((left, right) => left.Start.CompareTo(right.Start));
            foreach ((int start, int end) in spans)
            {
                if (_spans.Count > 0 && start <= _spans[^1].End)
                {
                    _spans[^1] = (_spans[^1].Start, Math.Max(_spans[^1].End, end));
                }
                else if (end > start)
                {
                    _spans.Add((start, end));
                }
            }
        }

        public bool Covers(int index) => EndOfSpanAt(index) >= 0;

        /// <summary>Gets the end of the span <paramref name="index"/> is in, or -1.</summary>
        public int EndOfSpanAt(int index)
        {
            int low = 0;
            int high = _spans.Count - 1;
            while (low <= high)
            {
                int middle = low + ((high - low) / 2);
                (int start, int end) = _spans[middle];
                if (index < start)
                {
                    high = middle - 1;
                }
                else if (index >= end)
                {
                    low = middle + 1;
                }
                else
                {
                    return end;
                }
            }

            return -1;
        }
    }
}
