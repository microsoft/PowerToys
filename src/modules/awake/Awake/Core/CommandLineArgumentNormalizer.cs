// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Awake.Core
{
    internal enum CommandLineNormalizationError
    {
        None,
        DurationRequired,
        InvalidDuration,
        TimeRequired,
        InvalidTime,
        TimeAlreadyPassed,
        ConflictingOptions,
    }

    internal static class CommandLineArgumentNormalizer
    {
        private const string ForCommand = "for";
        private const string UntilCommand = "until";
        private const string ParentAlias = "--parent";
        private const string ParentOption = "--use-parent-pid";

        internal static bool TryNormalize(
            string[] arguments,
            DateTimeOffset now,
            CultureInfo culture,
            out string[] normalizedArguments,
            out CommandLineNormalizationError error,
            out string? invalidValue)
        {
            ArgumentNullException.ThrowIfNull(arguments);
            ArgumentNullException.ThrowIfNull(culture);

            error = CommandLineNormalizationError.None;
            invalidValue = null;

            if (arguments.Length == 0)
            {
                normalizedArguments = [];
                return true;
            }

            if (IsHelpRequested(arguments))
            {
                normalizedArguments = ["--help"];
                return true;
            }

            List<string> normalized = arguments
                .Select(argument => argument.Equals(ParentAlias, StringComparison.OrdinalIgnoreCase) ? ParentOption : argument)
                .ToList();

            if (normalized[0].Equals(ForCommand, StringComparison.OrdinalIgnoreCase))
            {
                if (HasConflictingOptions(normalized))
                {
                    normalizedArguments = [];
                    error = CommandLineNormalizationError.ConflictingOptions;
                    return false;
                }

                if (normalized.Count < 2)
                {
                    normalizedArguments = [];
                    error = CommandLineNormalizationError.DurationRequired;
                    return false;
                }

                invalidValue = normalized[1];
                if (!TryParseDurationSeconds(invalidValue, out uint seconds))
                {
                    normalizedArguments = [];
                    error = CommandLineNormalizationError.InvalidDuration;
                    return false;
                }

                normalized.RemoveRange(0, 2);
                normalized.Insert(0, seconds.ToString(CultureInfo.InvariantCulture));
                normalized.Insert(0, "--time-limit");
            }
            else if (normalized[0].Equals(UntilCommand, StringComparison.OrdinalIgnoreCase))
            {
                if (HasConflictingOptions(normalized))
                {
                    normalizedArguments = [];
                    error = CommandLineNormalizationError.ConflictingOptions;
                    return false;
                }

                if (normalized.Count < 2)
                {
                    normalizedArguments = [];
                    error = CommandLineNormalizationError.TimeRequired;
                    return false;
                }

                invalidValue = normalized[1];
                if (!TryParseExpiration(invalidValue, now, culture, TimeZoneInfo.Local, out DateTimeOffset expiration))
                {
                    normalizedArguments = [];
                    error = CommandLineNormalizationError.InvalidTime;
                    return false;
                }

                if (expiration <= now)
                {
                    normalizedArguments = [];
                    error = CommandLineNormalizationError.TimeAlreadyPassed;
                    return false;
                }

                normalized.RemoveRange(0, 2);
                normalized.Insert(0, expiration.ToString("O", CultureInfo.InvariantCulture));
                normalized.Insert(0, "--expire-at");
            }

            normalizedArguments = normalized.ToArray();
            return true;
        }

        internal static bool TryParseDurationSeconds(string value, out uint seconds)
        {
            seconds = 0;
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            ReadOnlySpan<char> remaining = value.AsSpan().Trim();
            ulong total = 0;
            bool foundPart = false;

            while (!remaining.IsEmpty)
            {
                int digitCount = 0;
                while (digitCount < remaining.Length && char.IsAsciiDigit(remaining[digitCount]))
                {
                    digitCount++;
                }

                if (digitCount == 0 ||
                    digitCount == remaining.Length ||
                    !ulong.TryParse(remaining[..digitCount], NumberStyles.None, CultureInfo.InvariantCulture, out ulong amount))
                {
                    return false;
                }

                ulong multiplier = char.ToLowerInvariant(remaining[digitCount]) switch
                {
                    'd' => 24 * 60 * 60,
                    'h' => 60 * 60,
                    'm' => 60,
                    's' => 1,
                    _ => 0,
                };

                if (multiplier == 0 || amount > uint.MaxValue / multiplier)
                {
                    return false;
                }

                total += amount * multiplier;
                if (total > uint.MaxValue)
                {
                    return false;
                }

                foundPart = true;
                remaining = remaining[(digitCount + 1)..];
            }

            if (!foundPart || total == 0)
            {
                return false;
            }

            seconds = (uint)total;
            return true;
        }

        internal static bool TryParseExpiration(
            string value,
            DateTimeOffset now,
            CultureInfo culture,
            TimeZoneInfo timeZone,
            out DateTimeOffset expiration)
        {
            if (TimeOnly.TryParse(value, culture, DateTimeStyles.AllowWhiteSpaces, out TimeOnly time))
            {
                DateTime localNow = TimeZoneInfo.ConvertTime(now, timeZone).DateTime;
                DateTime localDateTime = localNow.Date.Add(time.ToTimeSpan());
                for (int day = 0; day < 2; day++)
                {
                    if (timeZone.IsInvalidTime(localDateTime))
                    {
                        if (localDateTime.Date == localNow.Date && localDateTime < localNow)
                        {
                            localDateTime = localDateTime.AddDays(1);
                            continue;
                        }

                        expiration = default;
                        return false;
                    }

                    TimeSpan[] offsets = timeZone.IsAmbiguousTime(localDateTime)
                        ? timeZone.GetAmbiguousTimeOffsets(localDateTime)
                        : [timeZone.GetUtcOffset(localDateTime)];
                    DateTimeOffset? nextOccurrence = null;
                    foreach (TimeSpan offset in offsets)
                    {
                        DateTimeOffset candidate = new(localDateTime, offset);
                        if (candidate > now && (nextOccurrence == null || candidate < nextOccurrence.Value))
                        {
                            nextOccurrence = candidate;
                        }
                    }

                    if (nextOccurrence.HasValue)
                    {
                        expiration = nextOccurrence.Value;
                        return true;
                    }

                    localDateTime = localDateTime.AddDays(1);
                }

                expiration = default;
                return false;
            }

            if (DateTime.TryParse(value, culture, DateTimeStyles.AllowWhiteSpaces, out DateTime localExpiration) &&
                localExpiration.Kind == DateTimeKind.Unspecified &&
                timeZone.IsInvalidTime(localExpiration))
            {
                expiration = default;
                return false;
            }

            return DateTimeOffset.TryParse(value, culture, DateTimeStyles.AllowWhiteSpaces, out expiration);
        }

        private static bool HasConflictingOptions(List<string> arguments)
        {
            return arguments.Skip(2).Any(argument =>
            {
                string option = argument.Split(':', '=')[0];
                return option is "--time-limit" or "-t" or "--expire-at" or "-e" or "--use-pt-config" or "-c";
            });
        }

        private static bool IsHelpRequested(string[] arguments)
        {
            return arguments[0].Equals(ForCommand, StringComparison.OrdinalIgnoreCase) ||
                   arguments[0].Equals(UntilCommand, StringComparison.OrdinalIgnoreCase)
                ? arguments.Skip(1).Any(argument => argument is "--help" or "-h" or "-?")
                : false;
        }
    }
}
