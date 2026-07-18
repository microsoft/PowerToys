// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;

using PowerAccent.Common;

namespace Microsoft.PowerToys.Settings.UI.ViewModels
{
    /// <summary>
    /// Pairs a displayable character with its Unicode name for use in the Quick Accent
    /// reference guide. The name is resolved once at construction time via
    /// <see cref="UnicodeHelper.GetCharacterName"/> and falls back to the character
    /// itself when unavailable.
    /// </summary>
    public sealed record CharacterModel
    {
        /// <summary>Gets the character string (may be a surrogate pair for characters above U+FFFF).</summary>
        public string Value { get; init; }

        /// <summary>Gets the value to display in the reference guide.</summary>
        public string DisplayValue { get; init; }

        /// <summary>
        /// Gets the tooltip text to display. This is the Unicode character name when
        /// available (e.g. "Latin Small Letter E With Acute"), otherwise the character
        /// itself.
        /// </summary>
        public string Tooltip { get; init; }

        /// <summary>
        /// Initializes a new instance of the <see cref="CharacterModel"/> class. The
        /// Unicode name is resolved automatically.
        /// </summary>
        public CharacterModel(string value)
        {
            Value = value;
            DisplayValue = ContainsOnlyCombiningMarks(value) ? $"◌{value}" : value;
            Tooltip = UnicodeHelper.GetCharacterName(value) ?? value;
        }

        private static bool ContainsOnlyCombiningMarks(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return false;
            }

            for (var index = 0; index < value.Length;)
            {
                var category = CharUnicodeInfo.GetUnicodeCategory(value, index);

                if (category is not UnicodeCategory.NonSpacingMark
                    and not UnicodeCategory.SpacingCombiningMark
                    and not UnicodeCategory.EnclosingMark)
                {
                    return false;
                }

                index += char.IsHighSurrogate(value[index]) ? 2 : 1;
            }

            return true;
        }
    }
}
