// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;

namespace Awake.Core;

internal static class ExpirationTimeParser
{
    internal static ExpirationTimeParseStatus TryParse(
        string? value,
        DateTimeOffset now,
        CultureInfo culture,
        TimeZoneInfo timeZone,
        out DateTimeOffset expirationDateTime)
    {
        expirationDateTime = default;

        if (string.IsNullOrWhiteSpace(value))
        {
            return ExpirationTimeParseStatus.InvalidFormat;
        }

        return ExpirationTimeParseStatus.InvalidFormat;
    }
}
