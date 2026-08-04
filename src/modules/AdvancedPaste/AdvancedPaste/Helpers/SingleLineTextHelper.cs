// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Text.RegularExpressions;

namespace AdvancedPaste.Helpers;

public static partial class SingleLineTextHelper
{
    [GeneratedRegex(
        "[ \\t]*(?:(?:\\r\\n|[\\n\\r\\u000B\\u000C\\u0085\\u2028\\u2029])[ \\t]*)+",
        RegexOptions.CultureInvariant | RegexOptions.NonBacktracking)]
    private static partial Regex LineBreakRegex();

    public static string Convert(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        if (text.Length == 0)
        {
            return string.Empty;
        }

        return LineBreakRegex().Replace(text, " ").Trim();
    }
}
