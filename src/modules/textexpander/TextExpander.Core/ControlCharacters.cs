// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

namespace Microsoft.PowerToys.TextExpander;

/// <summary>
/// Control characters in text that goes into an app. A copy can carry them unseen, such as a link that ends in Ctrl+O,
/// which a shell reads as "run this line and fetch the next". A terminal filters them out of a paste, but acts on them when
/// they're typed.
/// </summary>
public static class ControlCharacters
{
    /// <summary>
    /// Whether <paramref name="ch"/> is a C0 or C1 control character or DEL other than a tab or a line break: one that's
    /// removed from what was copied, and from text typed into a terminal.
    /// </summary>
    public static bool IsRemoved(char ch)
        => (ch < '\u0020' && ch is not ('\t' or '\n' or '\r')) || (ch >= '\u007F' && ch <= '\u009F');

    /// <summary>
    /// Gets <paramref name="text"/> without the characters <see cref="IsRemoved"/> picks, keeping tabs and line breaks. The
    /// same string when there are none.
    /// </summary>
    public static string Remove(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        int first = 0;
        while (first < text.Length && !IsRemoved(text[first]))
        {
            first++;
        }

        if (first == text.Length)
        {
            return text;
        }

        return string.Create(text.Length - Count(text, first), (text, first), static (kept, state) =>
        {
            state.text.AsSpan(0, state.first).CopyTo(kept);
            int written = state.first;
            for (int i = state.first; i < state.text.Length; i++)
            {
                if (!IsRemoved(state.text[i]))
                {
                    kept[written++] = state.text[i];
                }
            }
        });
    }

    /// <summary>Whether <paramref name="text"/> has any control character at all, a tab or a line break included.</summary>
    public static bool HasAny(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        foreach (char ch in text)
        {
            if (char.IsControl(ch))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Whether <paramref name="text"/> has any character <see cref="IsRemoved"/> picks.</summary>
    public static bool HasRemoved(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        foreach (char ch in text)
        {
            if (IsRemoved(ch))
            {
                return true;
            }
        }

        return false;
    }

    private static int Count(string text, int from)
    {
        int count = 0;
        for (int i = from; i < text.Length; i++)
        {
            if (IsRemoved(text[i]))
            {
                count++;
            }
        }

        return count;
    }
}
