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

            if (isTimeOnly)
            {
                DateTimeOffset localNow = TimeZoneInfo.ConvertTime(now, timeZone);
                DateTime todayLocal = localNow.Date + parsedNoDefault.TimeOfDay;
                DateTimeOffset candidate = new(todayLocal, timeZone.GetUtcOffset(todayLocal));

                if (candidate <= now)
                {
                    if (localNow.Date >= DateTime.MaxValue.Date)
                    {
                        return ExpirationTimeParseStatus.InvalidFormat;
                    }

                    DateTime tomorrowLocal = localNow.Date.AddDays(1) + parsedNoDefault.TimeOfDay;
                    candidate = new DateTimeOffset(tomorrowLocal, timeZone.GetUtcOffset(tomorrowLocal));
                }

                expirationDateTime = candidate;
                return ExpirationTimeParseStatus.Success;
            }

            try
            {
                DateTimeOffset localCandidate = new(parsedNoDefault, timeZone.GetUtcOffset(parsedNoDefault));
                if (localCandidate <= now)
                {
                    return ExpirationTimeParseStatus.InThePast;
                }

                expirationDateTime = localCandidate;
                return ExpirationTimeParseStatus.Success;
            }
            catch (ArgumentOutOfRangeException)
            {
                return parsedNoDefault.Year == 1
                    ? ExpirationTimeParseStatus.InThePast
                    : ExpirationTimeParseStatus.InvalidFormat;
            }
        }

        if (!DateTimeOffset.TryParse(value, culture, DateTimeStyles.AllowWhiteSpaces, out DateTimeOffset parsedOffset))
        {
            return ExpirationTimeParseStatus.InvalidFormat;
        }

        if (parsedOffset <= now)
        {
            return ExpirationTimeParseStatus.InThePast;
        }

        expirationDateTime = parsedOffset;
        return ExpirationTimeParseStatus.Success;
    }
}
