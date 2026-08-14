// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace KeyboardManagerEditorUI.Helpers
{
    internal static class TextReplacementTextValidator
    {
        internal static bool IsValidTarget(string text)
        {
            for (int index = 0; index < text.Length; ++index)
            {
                char value = text[index];
                if (char.IsHighSurrogate(value))
                {
                    if (++index >= text.Length || !char.IsLowSurrogate(text[index]))
                    {
                        return false;
                    }
                }
                else if (char.IsLowSurrogate(value) ||
                         (value < '\u0020' && value != '\r' && value != '\n') ||
                         (value >= '\u007F' && value <= '\u009F'))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
