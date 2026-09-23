// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.RegularExpressions;

namespace Microsoft.CommandPalette.Extensions.Toolkit;

internal static class RegularExpressionPattern
{
    private static readonly TimeSpan _matchTimeout = TimeSpan.FromMilliseconds(250);

    public static string Validate(string? pattern, string propertyName)
    {
        if (string.IsNullOrEmpty(pattern))
        {
            return string.Empty;
        }

        try
        {
            _ = new Regex(pattern, RegexOptions.CultureInvariant, _matchTimeout);
            return pattern;
        }
        catch (ArgumentException ex)
        {
            throw new ArgumentException("The validation pattern must be a valid regular expression.", propertyName, ex);
        }
    }
}
