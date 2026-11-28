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
            out DateTime parsedNoDefault))
        {
            // Compare with and without NoCurrentDateDefault to exclude literal 0001-01-01 dates
            // while keeping time-only offsets that cross midnight onto 0001-01-02.
            bool isTimeOnly = DateTime.TryParse(value, culture, DateTimeStyles.AllowWhiteSpaces, out DateTime parsedWithDefault)
                && parsedNoDefault.Date != parsedWithDefault.Date;

            if (isTimeOnly)
            {
                try
                {
                    DateTimeOffset anchoredNow;
                    TimeSpan timeOfDay;
                    TimeSpan? fixedOffset = null;

                    if (parsedNoDefault.Kind == DateTimeKind.Unspecified)
                    {
                        anchoredNow = TimeZoneInfo.ConvertTime(now, timeZone);
                        timeOfDay = parsedNoDefault.TimeOfDay;
                    }
                    else
                    {
                        if (!DateTimeOffset.TryParse(value, culture, DateTimeStyles.AllowWhiteSpaces, out DateTimeOffset parsedTimeOffset))
                        {
                            return ExpirationTimeParseStatus.InvalidFormat;
                        }

                        anchoredNow = now.ToOffset(parsedTimeOffset.Offset);
                        timeOfDay = parsedTimeOffset.TimeOfDay;
                        fixedOffset = parsedTimeOffset.Offset;
                    }

                    DateTime todayDateTime = anchoredNow.Date + timeOfDay;
                    DateTimeOffset candidate = new(todayDateTime, fixedOffset ?? timeZone.GetUtcOffset(todayDateTime));

                    if (candidate <= now)
                    {
                        DateTime tomorrowDateTime = anchoredNow.Date.AddDays(1) + timeOfDay;
                        candidate = new DateTimeOffset(tomorrowDateTime, fixedOffset ?? timeZone.GetUtcOffset(tomorrowDateTime));
                    }

                    expirationDateTime = candidate;
                    return ExpirationTimeParseStatus.Success;
                }
                catch (ArgumentOutOfRangeException)
                {
                    return ExpirationTimeParseStatus.InvalidFormat;
                }
            }

            if (parsedNoDefault.Kind == DateTimeKind.Unspecified)
            {
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
