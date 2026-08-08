// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;

namespace Microsoft.CmdPal.UI.ViewModels.Auth;

/// <summary>
/// Parses an <c>application/x-www-form-urlencoded</c> query string (without the
/// leading '?') into a case-sensitive dictionary. Later duplicate keys win.
/// </summary>
internal static class QueryStringParser
{
    public static Dictionary<string, string> Parse(string? query)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        if (string.IsNullOrEmpty(query))
        {
            return result;
        }

        foreach (var pair in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var eq = pair.IndexOf('=');
            string key;
            string value;
            if (eq < 0)
            {
                key = Decode(pair);
                value = string.Empty;
            }
            else
            {
                key = Decode(pair[..eq]);
                value = Decode(pair[(eq + 1)..]);
            }

            if (key.Length > 0)
            {
                result[key] = value;
            }
        }

        return result;
    }

    private static string Decode(string value) =>
        Uri.UnescapeDataString(value.Replace('+', ' '));
}
