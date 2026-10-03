// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    /// <summary>
    /// Parses and validates the per-layout default zone (or inclusive zone range) a newly created
    /// window with no zone history falls back to. Zone numbers here are 1-based, matching what is
    /// already shown to the user elsewhere in FancyZones (e.g. the on-screen zone number overlay);
    /// the parsed result is converted to the 0-based zone-index-set representation used internally.
    /// </summary>
    public static class FancyZonesDefaultZoneValidator
    {
        public enum ValidationResult
        {
            Valid,
            Empty,
            NotANumber,
            ReversedRange,
            OutOfRange,
        }

        /// <summary>
        /// Parses a single zone number ("3") or an inclusive range ("2-4") and validates it against
        /// the layout's zone count. On success, <paramref name="zoneIndexSet"/> holds the 0-based
        /// indexes to persist; on failure it is null.
        /// </summary>
        public static ValidationResult TryParse(string text, int zoneCount, out List<int> zoneIndexSet)
        {
            zoneIndexSet = null;

            if (string.IsNullOrWhiteSpace(text))
            {
                return ValidationResult.Empty;
            }

            text = text.Trim();
            int dashIndex = text.IndexOf('-');

            int firstOneBased;
            int lastOneBased;

            if (dashIndex > 0)
            {
                string firstPart = text.Substring(0, dashIndex).Trim();
                string lastPart = text.Substring(dashIndex + 1).Trim();

                if (!int.TryParse(firstPart, out firstOneBased) || !int.TryParse(lastPart, out lastOneBased))
                {
                    return ValidationResult.NotANumber;
                }
            }
            else
            {
                if (!int.TryParse(text, out firstOneBased))
                {
                    return ValidationResult.NotANumber;
                }

                lastOneBased = firstOneBased;
            }

            if (lastOneBased < firstOneBased)
            {
                return ValidationResult.ReversedRange;
            }

            if (firstOneBased < 1 || lastOneBased > zoneCount)
            {
                return ValidationResult.OutOfRange;
            }

            var result = new List<int>();
            for (int oneBased = firstOneBased; oneBased <= lastOneBased; oneBased++)
            {
                result.Add(oneBased - 1);
            }

            zoneIndexSet = result;
            return ValidationResult.Valid;
        }
    }
}
