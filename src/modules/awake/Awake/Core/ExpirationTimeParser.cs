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
            // Distinguish literal 0001-01-01 dates from time-only strings that default to DateTime.MinValue.
            bool isTimeOnly = parsedNoDefault.Date == DateTime.MinValue
                && DateTime.TryParse(value, culture, DateTimeStyles.AllowWhiteSpaces, out DateTime parsedWithDefault)
                && parsedWithDefault.Date != DateTime.MinValue;

            return isTimeOnly
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
        expirationDateTime = default;

        TimeSpan offset = timeZone.GetUtcOffset(localDateTime);
        long utcTicks = localDateTime.Ticks - offset.Ticks;
        if (utcTicks < DateTimeOffset.MinValue.Ticks)
        {
            return ExpirationTimeParseStatus.InThePast;
        }

        if (utcTicks > DateTimeOffset.MaxValue.Ticks)
        {
            return ExpirationTimeParseStatus.InvalidFormat;
        }

        DateTimeOffset resolved = new(localDateTime, offset);
        if (resolved <= now)
        {
            return ExpirationTimeParseStatus.InThePast;
        }

        expirationDateTime = resolved;
        return ExpirationTimeParseStatus.Success;
    }
}
