// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Microsoft.PowerToys.Settings.UI.Helpers
{
    /// <summary>
    /// Sizes the column of trigger keycaps in the snippet list to its widest keycap, so the
    /// descriptions line up across rows without a wide empty gap.
    /// </summary>
    internal static class TextExpanderKeycapLayout
    {
        /// <summary>
        /// Space a keycap adds around its trigger: 10 px padding on each side, the 1 px border, and
        /// 2 px so layout rounding at display scales such as 125% never trims a trigger that fits.
        /// </summary>
        public const double Chrome = (2 * 10) + 2 + 2;

        /// <summary>The narrowest column, so a one-character trigger still looks like a key.</summary>
        public const double MinWidth = 32;

        /// <summary>The widest column. Longer triggers are trimmed, with the whole trigger in a tooltip.</summary>
        public const double MaxWidth = 140;

        /// <summary>How many triggers are measured: the ones that look widest, as the font is monospace.</summary>
        public const int MeasuredTriggers = 8;

        /// <summary>Gets the column width for triggers whose text measures <paramref name="triggerTextWidths"/>.</summary>
        public static double ColumnWidth(IEnumerable<double> triggerTextWidths)
        {
            double widest = 0;
            foreach (double width in triggerTextWidths ?? Array.Empty<double>())
            {
                if (double.IsFinite(width) && width > widest)
                {
                    widest = width;
                }
            }

            return Math.Clamp(Math.Ceiling(widest + Chrome), MinWidth, MaxWidth);
        }

        /// <summary>
        /// Gets the triggers that can be the widest, so only they have to be measured rather than
        /// the whole list. Consolas gives every character the same width, except East Asian wide
        /// characters and emoji, which take two cells, and combining marks, which take none, so
        /// counting cells finds the widest; a few more are kept for fallback fonts.
        /// </summary>
        public static IReadOnlyList<string> WidestTriggers(IEnumerable<string> triggers, int count = MeasuredTriggers)
        {
            // Widest first; a trigger as wide as one already kept goes after it.
            var widest = new List<(int Cells, string Trigger)>(count + 1);
            foreach (string trigger in triggers ?? Array.Empty<string>())
            {
                int cells = Cells(trigger);
                if (widest.Count == count && cells <= widest[^1].Cells)
                {
                    continue;
                }

                int at = widest.Count;
                while (at > 0 && widest[at - 1].Cells < cells)
                {
                    at--;
                }

                widest.Insert(at, (cells, trigger));
                if (widest.Count > count)
                {
                    widest.RemoveAt(count);
                }
            }

            return widest.ConvertAll(entry => entry.Trigger);
        }

        /// <summary>Gets how many monospace cells <paramref name="text"/> takes, roughly as a terminal counts them.</summary>
        internal static int Cells(string text)
        {
            int cells = 0;
            foreach (Rune rune in (text ?? string.Empty).EnumerateRunes())
            {
                UnicodeCategory category = Rune.GetUnicodeCategory(rune);
                if (category is UnicodeCategory.NonSpacingMark or UnicodeCategory.EnclosingMark or UnicodeCategory.Format)
                {
                    continue;
                }

                cells += IsWide(rune.Value) ? 2 : 1;
            }

            return cells;
        }

        // East Asian wide and full-width ranges, and emoji.
        private static bool IsWide(int value) => value is (>= 0x1100 and <= 0x115F)
            or (>= 0x2E80 and <= 0xA4CF)
            or (>= 0xAC00 and <= 0xD7A3)
            or (>= 0xF900 and <= 0xFAFF)
            or (>= 0xFE30 and <= 0xFE4F)
            or (>= 0xFF00 and <= 0xFF60)
            or (>= 0xFFE0 and <= 0xFFE6)
            or >= 0x1F300;
    }
}
