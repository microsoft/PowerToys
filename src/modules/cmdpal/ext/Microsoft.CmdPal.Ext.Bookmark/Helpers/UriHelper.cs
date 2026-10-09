// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.Ext.Bookmarks.Helpers;

internal static class UriHelper
{
    /// <summary>
    /// Tries to split a URI string into scheme and remainder.
    /// Scheme must be valid per RFC 3986 and followed by ':'.
    /// </summary>
    public static bool TryGetScheme(ReadOnlySpan<char> input, out string scheme, out string remainder)
    {
        // https://datatracker.ietf.org/doc/html/rfc3986#page-17
        scheme = string.Empty;
        remainder = string.Empty;

        if (input.Length < 2)
        {
            return false; // must have at least "a:"
        }

        // Must contain ':' delimiter
        var colonIndex = input.IndexOf(':');
        if (colonIndex <= 0)
        {
            return false; // no colon or colon at start
        }

        // First char must be a letter
        var first = input[0];
        if (!char.IsLetter(first))
        {
            return false;
        }

        // Validate scheme part
        for (var i = 1; i < colonIndex; i++)
        {
            var c = input[i];
            if (!(char.IsLetterOrDigit(c) || c == '+' || c == '-' || c == '.'))
            {
                return false;
            }
        }

        // Extract scheme and remainder
        scheme = input[..colonIndex].ToString();
        remainder = colonIndex + 1 < input.Length ? input[(colonIndex + 1)..].ToString() : string.Empty;
        return true;
    }
}
