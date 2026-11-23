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

        if (DateTime.TryParse(
            value,
            culture,
            DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.NoCurrentDateDefault,
            out DateTime parsedNoDefault) && parsedNoDefault.Kind == DateTimeKind.Unspecified)
        {
            return parsedNoDefault.Date == DateTime.MinValue
                ? ExpirationTimeParseStatus.InvalidFormat
                : TryResolveLocalDateTime(parsedNoDefault, now, timeZone, out expirationDateTime);
        }

        if (!DateTimeOffset.TryParse(value, culture, DateTimeStyles.AllowWhiteSpaces, out DateTimeOffset parsedOffset))
        {
            return ExpirationTimeParseStatus.InvalidFormat;
        }

        if (parsedOffset <= now)
        {
            expirationDateTime = default;
            return ExpirationTimeParseStatus.InThePast;
        }

        expirationDateTime = parsedOffset;
        return ExpirationTimeParseStatus.Success;
    }

    private static ExpirationTimeParseStatus TryResolveLocalDateTime(
        DateTime localDateTime,
        DateTimeOffset now,
        TimeZoneInfo timeZone,
        out DateTimeOffset expirationDateTime)
    {
        DateTimeOffset resolved = new(localDateTime, timeZone.GetUtcOffset(localDateTime));
        if (resolved <= now)
        {
            expirationDateTime = default;
            return ExpirationTimeParseStatus.InThePast;
        }

        expirationDateTime = resolved;
        return ExpirationTimeParseStatus.Success;
    }
}
