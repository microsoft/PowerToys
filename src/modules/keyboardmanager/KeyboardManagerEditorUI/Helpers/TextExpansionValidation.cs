// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;

namespace KeyboardManagerEditorUI.Helpers
{
    public static class TextExpansionValidation
    {
        public const int MaxTextLength = 256;

        public static bool IsValidSourceText(string? text) => IsValidRequiredText(text, allowLineBreaks: false);

        public static bool IsValidReplacementText(string? text) => IsValidRequiredText(text, allowLineBreaks: true);

        public static bool IsValidActivationKeys(IReadOnlyList<int>? keys)
        {
            if (keys == null || keys.Count == 0 || keys.Count > 5)
            {
                return false;
            }

            int actionKeyCount = 0;
            var modifierFamilies = new HashSet<int>();

            foreach (int key in keys)
            {
                if (key <= 0 || (key > 0xFF && key != 0x104))
                {
                    return false;
                }

                int modifierFamily = GetModifierFamily(key);
                if (modifierFamily == 0)
                {
                    actionKeyCount++;
                    if (actionKeyCount > 1)
                    {
                        return false;
                    }
                }
                else if (!modifierFamilies.Add(modifierFamily))
                {
                    return false;
                }
            }

            return actionKeyCount == 1;
        }

        public static bool IsCanonicalGuid(string? id)
        {
            return Guid.TryParseExact(id, "D", out Guid parsed) &&
                   string.Equals(id, parsed.ToString("D"), StringComparison.Ordinal);
        }

        private static bool IsValidRequiredText(string? text, bool allowLineBreaks)
        {
            if (string.IsNullOrEmpty(text) || text.Length > MaxTextLength)
            {
                return false;
            }

            for (int index = 0; index < text.Length; index++)
            {
                char current = text[index];
                if ((current < 0x20 && !(allowLineBreaks && (current == '\r' || current == '\n'))) ||
                    (current >= 0x7F && current <= 0x9F))
                {
                    return false;
                }

                if (char.IsHighSurrogate(current))
                {
                    if (index + 1 >= text.Length || !char.IsLowSurrogate(text[index + 1]))
                    {
                        return false;
                    }

                    index++;
                }
                else if (char.IsLowSurrogate(current))
                {
                    return false;
                }
            }

            return true;
        }

        private static int GetModifierFamily(int key)
        {
            return key switch
            {
                0x5B or 0x5C or 0x104 => 1, // Win
                0x11 or 0xA2 or 0xA3 => 2, // Ctrl
                0x12 or 0xA4 or 0xA5 => 3, // Alt
                0x10 or 0xA0 or 0xA1 => 4, // Shift
                _ => 0,
            };
        }
    }
}
