// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;

namespace Microsoft.PowerToys.Settings.UI.Helpers
{
    /// <summary>
    /// The rules behind Insert variable's ready-made date formats and the snippet editor's date
    /// format guide: which formats and codes they offer, how each comes out (formatted the way
    /// the engine formats {{date:…}}), when the guide shows, and where a code from it goes.
    /// </summary>
    internal static class TextExpanderDateGuide
    {
        private const string Prefix = "{{date:";
        private const string Closing = "}}";

        /// <summary>Gets the ready-made formats under Insert variable &gt; Custom date format, in menu order.</summary>
        public static IReadOnlyList<string> Presets { get; } = new[]
        {
            "dddd", "MMMM d, yyyy", "dddd, MMMM d, yyyy", "MMM d", "MM/dd/yyyy", "yyyy-MM-dd", "h:mm tt", "HH:mm",
        };

        /// <summary>
        /// Gets the guide's codes, a row each for years, months, days and times: each with the
        /// resource that says what it is, and for an hour the one that labels its clock.
        /// </summary>
        public static IReadOnlyList<IReadOnlyList<(string Code, string MeaningKey, string ExampleKey)>> Rows { get; } = new[]
        {
            new[]
            {
                ("yyyy", "TextExpander_DateCode_Year", (string)null),
                ("yy", "TextExpander_DateCode_YearShort", null),
            },
            new[]
            {
                ("MMMM", "TextExpander_DateCode_Month", (string)null),
                ("MMM", "TextExpander_DateCode_MonthShort", null),
                ("MM", "TextExpander_DateCode_MonthNumberTwoDigits", null),
                ("M", "TextExpander_DateCode_MonthNumber", null),
            },
            new[]
            {
                ("dddd", "TextExpander_DateCode_Weekday", (string)null),
                ("ddd", "TextExpander_DateCode_WeekdayShort", null),
                ("dd", "TextExpander_DateCode_DayTwoDigits", null),
                ("d", "TextExpander_DateCode_Day", null),
            },
            new[]
            {
                ("h", "TextExpander_DateCode_Hour12", "TextExpander_DateCode_Hour12Example"),
                ("HH", "TextExpander_DateCode_Hour24", "TextExpander_DateCode_Hour24Example"),
                ("mm", "TextExpander_DateCode_Minutes", (string)null),
                ("tt", "TextExpander_DateCode_AmPm", null),
            },
        };

        /// <summary>Gets the variable that types <paramref name="format"/>.</summary>
        public static string Token(string format) => Prefix + format + Closing;

        /// <summary>Gets what <paramref name="format"/> gives at <paramref name="now"/>, the way the engine formats it.</summary>
        public static string Example(string format, DateTime now) => TextExpanderSnippetPreview.FormatDate(now, format ?? string.Empty);

        /// <summary>Gets a ready-made format's accessible name: what it gives now, and its code.</summary>
        public static string PresetName(string format, DateTime now, Func<string, string> resource)
            => string.Format(CultureInfo.CurrentCulture, resource("TextExpander_DatePreset_Name"), Example(format, now), format);

        /// <summary>Gets what a code's chip shows next to it: what it gives now, and for an hour, which clock.</summary>
        public static string CodeExample((string Code, string MeaningKey, string ExampleKey) code, DateTime now, Func<string, string> resource)
            => code.ExampleKey == null
                ? Example(code.Code, now)
                : string.Format(CultureInfo.CurrentCulture, resource(code.ExampleKey), Example(code.Code, now));

        /// <summary>Gets a code's accessible name, such as "yyyy, year, 2026".</summary>
        public static string CodeName((string Code, string MeaningKey, string ExampleKey) code, DateTime now, Func<string, string> resource)
            => string.Format(CultureInfo.CurrentCulture, resource("TextExpander_DateCode_Name"), code.Code, resource(code.MeaningKey), Example(code.Code, now));

        /// <summary>Gets the guide's line saying what <paramref name="format"/> shows now, or empty when it shows nothing.</summary>
        public static string ShowsAs(string format, DateTime now, Func<string, string> resource)
        {
            string example = Example(format, now);
            return example.Length == 0 ? string.Empty : string.Format(CultureInfo.CurrentCulture, resource("TextExpander_DateGuide_ShowsAs"), example);
        }

        /// <summary>
        /// Gets the {{date:…}} variable that the caret, or the whole selection, is in: where it
        /// starts and ends, where its format starts, and the format. Null anywhere else. A caret
        /// just outside its braces isn't in it, but the whole variable selected is, as a click on
        /// one selects it.
        /// </summary>
        public static (int Start, int End, int FormatStart, string Format)? TokenAround(string text, int selectionStart, int selectionEnd)
        {
            text ??= string.Empty;
            foreach ((int start, int end) in TextExpanderTokenText.FindVariables(text))
            {
                if (string.CompareOrdinal(text, start, Prefix, 0, Prefix.Length) != 0)
                {
                    continue;
                }

                bool inside = selectionStart == selectionEnd
                    ? start < selectionStart && selectionStart < end
                    : start <= selectionStart && selectionEnd <= end;
                if (inside)
                {
                    int formatStart = start + Prefix.Length;
                    return (start, end, formatStart, text[formatStart..(end - Closing.Length)]);
                }
            }

            return null;
        }

        /// <summary>
        /// Gets where a code from the guide goes, and the caret after it: in place of the selection
        /// when that's within the format, or else at the end of the format, so the braces are never
        /// touched. Null when the selection isn't in a {{date:…}} variable.
        /// </summary>
        public static (int Start, int End, int Caret)? CodeInsertion(string text, int selectionStart, int selectionEnd, string code)
        {
            if (TokenAround(text, selectionStart, selectionEnd) is not { } token)
            {
                return null;
            }

            int formatEnd = token.End - Closing.Length;
            (int start, int end) = selectionStart >= token.FormatStart && selectionEnd <= formatEnd
                ? (selectionStart, selectionEnd)
                : (formatEnd, formatEnd);
            return (start, end, start + code.Length);
        }
    }
}
