// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System.Buffers;

namespace Microsoft.PowerToys.TextExpander;

/// <summary>
/// Control characters in text that goes into an app. A copy can carry them unseen, such as a link that ends in Ctrl+O,
/// which a shell reads as "run this line and fetch the next". A terminal filters them out of a paste, but acts on them when
/// they're typed.
/// </summary>
public static class ControlCharacters
{
    // Looked for many at a time, since a copy can run to a million characters and a pick page reads it once for each match.
    private static readonly SearchValues<char> Removed = SearchValues.Create(
        "\u0000\u0001\u0002\u0003\u0004\u0005\u0006\u0007\u0008\u000B\u000C\u000E\u000F\u0010\u0011\u0012\u0013\u0014\u0015\u0016\u0017\u0018\u0019\u001A\u001B\u001C\u001D\u001E\u001F"
        + "\u007F\u0080\u0081\u0082\u0083\u0084\u0085\u0086\u0087\u0088\u0089\u008A\u008B\u008C\u008D\u008E\u008F\u0090\u0091\u0092\u0093\u0094\u0095\u0096\u0097\u0098\u0099\u009A\u009B\u009C\u009D\u009E\u009F");

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

        int first = text.AsSpan().IndexOfAny(Removed);
        if (first < 0)
        {
            return text;
        }

        return string.Create(text.Length - Count(text, first), text, static (kept, text) =>
        {
            ReadOnlySpan<char> rest = text;
            int written = 0;
            for (int next = rest.IndexOfAny(Removed); next >= 0; next = rest.IndexOfAny(Removed))
            {
                rest[..next].CopyTo(kept[written..]);
                written += next;
                rest = rest[(next + 1)..];
            }

            rest.CopyTo(kept[written..]);
        });
    }

    /// <summary>Whether <paramref name="text"/> has any control character at all, a tab or a line break included.</summary>
    public static bool HasAny(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        return text.AsSpan().IndexOfAnyInRange('\u0000', '\u001F') >= 0 || text.AsSpan().IndexOfAnyInRange('\u007F', '\u009F') >= 0;
    }

    /// <summary>Whether <paramref name="text"/> has any character <see cref="IsRemoved"/> picks.</summary>
    public static bool HasRemoved(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        return text.AsSpan().ContainsAny(Removed);
    }

    private static int Count(string text, int first)
    {
        int count = 0;
        ReadOnlySpan<char> rest = text.AsSpan(first);
        for (int next = rest.IndexOfAny(Removed); next >= 0; next = rest.IndexOfAny(Removed))
        {
            count++;
            rest = rest[(next + 1)..];
        }

        return count;
    }
}
