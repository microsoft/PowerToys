// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics.CodeAnalysis;

#nullable enable

namespace FancyZonesCLI.Utils;

/// <summary>
/// Helper class for normalizing GUID strings to Windows format with braces.
/// Supports input with or without braces: both "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx"
/// and "{xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx}" are accepted.
/// </summary>
internal static class GuidHelper
{
    /// <summary>
    /// Normalizes a GUID string to Windows format with braces.
    /// Returns null if the input is not a valid GUID.
    /// </summary>
    /// <param name="input">GUID string with or without braces.</param>
    /// <returns>GUID in "{xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx}" format, or null if invalid.</returns>
    public static string? NormalizeGuid(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return null;
        }

        if (Guid.TryParse(input, out Guid guid))
        {
            // "B" format includes braces: {xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx}
            return guid.ToString("B").ToUpperInvariant();
        }

        return null;
    }

    /// <summary>
    /// Tries to normalize a GUID string to Windows format with braces.
    /// </summary>
    /// <param name="input">GUID string with or without braces.</param>
    /// <param name="normalizedGuid">The normalized GUID string, or the original input if normalization fails.</param>
    /// <returns>True if the input was successfully normalized; otherwise, false.</returns>
    public static bool TryNormalizeGuid(string? input, [NotNullWhen(true)] out string? normalizedGuid)
    {
        normalizedGuid = NormalizeGuid(input);
        return normalizedGuid != null;
    }
}
