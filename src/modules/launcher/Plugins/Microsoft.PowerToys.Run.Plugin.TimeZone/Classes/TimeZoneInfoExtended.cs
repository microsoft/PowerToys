// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Security;
using System.Text;
using Wox.Plugin.Logger;

namespace Microsoft.PowerToys.Run.Plugin.TimeZone
{
    /// <summary>
    /// A extended version of <see cref="TimeZoneInfo"/>
    /// with additional fields to easier find a time zone.
    /// </summary>
    internal sealed class TimeZoneInfoExtended
    {
        /// <summary>
        /// The underling <see cref="TimeZoneInfo"/> of this <see cref="TimeZoneInfoExtended"/>.
        /// </summary>
        private TimeZoneInfo? _underlyingTimeZone;

        /// <summary>
        /// Initializes a new instance of the <see cref="TimeZoneInfoExtended"/> class.
        /// </summary>
        public TimeZoneInfoExtended()
        {
            DisplayName = string.Empty;
            StandardName = string.Empty;
            DaylightName = string.Empty;
            Id = string.Empty;
            Offset = string.Empty;
            StandardShortcut = string.Empty;
            DaylightShortcut = string.Empty;
        }

        // TODO: Remove after JSON is used
        internal TimeZoneInfoExtended(TimeZoneInfo timeZone)
        {
            Id = timeZone.Id;

            Offset = $"{timeZone.BaseUtcOffset:hh\\:mm}";

            if (Offset.StartsWith("0", StringComparison.InvariantCultureIgnoreCase))
            {
                Offset = Offset.Substring(1);
            }

            if (!Offset.StartsWith("+", StringComparison.InvariantCultureIgnoreCase))
            {
                Offset = $"+{Offset}";
            }

            StandardName = timeZone.StandardName;
            StandardShortcut = GetShortcut(StandardName);
            DisplayName = timeZone.DisplayName;

            if (!timeZone.SupportsDaylightSavingTime)
            {
                DaylightName = timeZone.DaylightName;
                DaylightShortcut = GetShortcut(DaylightName);
            }
        }

        // TODO: Remove after JSON is used
        private static string? GetShortcut(string name)
        {
            if (name.StartsWith("UTC", StringComparison.InvariantCultureIgnoreCase))
            {
                return null;
            }

            var result = new StringBuilder();

            foreach (var ch in name)
            {
                if (ch >= 'A' && ch <= 'Z')
                {
                    result.Append(ch);
                }
            }

            if (result.Length == 0)
            {
                return null;
            }

            return result.ToString();
        }

        /// <summary>
        /// Gets or sets the general display name that represents the time zone.
        /// </summary>
        internal string DisplayName { get; set; }

        /// <summary>
        /// Gets or sets the search-able time difference between the current time zone's standard time and Coordinated Universal Time (UTC).
        /// </summary>
        public string Offset { get; set; }

        /// <summary>
        /// Gets or sets the time zone identifier.
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// Gets or sets the display name for the time zone's standard time.
        /// </summary>
        public string StandardName { get; set; }

        /// <summary>
        /// Gets or sets the shortcut of the display name for the time zone's standard time.
        /// </summary>
        public string? StandardShortcut { get; set; }

        /// <summary>
        ///  Gets or sets the display name for the current time zone's daylight saving time.
        /// </summary>
        public string? DaylightName { get; set; }

        /// <summary>
        /// Gets or sets the shortcut of the display name for the current time zone's daylight saving time.
        /// </summary>
        public string? DaylightShortcut { get; set; }

        /// <summary>
        /// Indicates whether a specified date and time falls in the range of daylight saving
        ///  time for the time zone of the current System.TimeZoneInfo object.
        /// </summary>
        /// <param name="dateTime">A date and time value.</param>
        /// <returns><see langword="true"/> if the dateTime parameter is a daylight saving time; otherwise, <see langword="false"/>.</returns>
        internal bool IsDaylightSavingTime(DateTime dateTime)
        {
            var result = false;

            FindTimeZone();
            if (_underlyingTimeZone is null)
            {
                return result;
            }

            if (!_underlyingTimeZone.SupportsDaylightSavingTime)
            {
                return result;
            }

            try
            {
                result = _underlyingTimeZone.IsDaylightSavingTime(dateTime);
            }
            catch (ArgumentException exception)
            {
                Log.Exception(
                    "The System.DateTime.Kind property of the dateTime value is System.DateTimeKind.Local and dateTime is an invalid time.",
                    exception,
                    typeof(TimeZoneInfoExtended));
            }

            return result;
        }

        /// <summary>
        ///  Converts a time to the time in a particular time zone.
        /// </summary>
        /// <param name="dateTime">The date and time to convert.</param>
        /// <returns>The date and time in this time zone.</returns>
        internal DateTime ConvertTime(DateTime dateTime)
        {
            var result = DateTime.MinValue;

            FindTimeZone();
            if (_underlyingTimeZone is null)
            {
                return result;
            }

            try
            {
                result = TimeZoneInfo.ConvertTime(dateTime, _underlyingTimeZone);
            }
            catch (ArgumentNullException exception)
            {
                Log.Exception(
                    "The value of the destinationTimeZone parameter is null.",
                    exception,
                    typeof(TimeZoneInfoExtended));
            }
            catch (ArgumentException exception)
            {
                Log.Exception(
                    "The value of the dateTime parameter represents an invalid time.",
                    exception,
                    typeof(TimeZoneInfoExtended));
            }

            return result;
        }

        /// <summary>
        /// Try to find a <see cref="TimeZoneInfo"/> that have the same <see cref="Id"/>
        /// <para>The <see cref="TimeZoneInfo"/> is need for the time calculation.</para>
        /// </summary>
        internal void FindTimeZone()
        {
            if (_underlyingTimeZone is null)
            {
                try
                {
                    _underlyingTimeZone = TimeZoneInfo.FindSystemTimeZoneById(Id);
                }
                catch (OutOfMemoryException exception)
                {
                    Log.Exception(
                        "The system does not have enough memory to hold information about the time zone.",
                        exception,
                        typeof(TimeZoneInfoExtended));
                }
                catch (ArgumentNullException exception)
                {
                    Log.Exception(
                        "The id parameter is null.",
                        exception,
                        typeof(TimeZoneInfoExtended));
                }
                catch (TimeZoneNotFoundException exception)
                {
                    Log.Exception(
                        "The time zone identifier specified by id was not found."
                        + " This means that a time zone identifier whose name matches id does not exist,"
                        + " or that the identifier exists but does not contain any time zone data.",
                        exception,
                        typeof(TimeZoneInfoExtended));
                }
                catch (SecurityException exception)
                {
                    Log.Exception(
                        "The process does not have the permissions required to read from the registry key"
                        + " that contains the time zone information.",
                        exception,
                        typeof(TimeZoneInfoExtended));
                }
                catch (InvalidTimeZoneException exception)
                {
                    Log.Exception(
                        "The time zone identifier was found, but the registry data is corrupted.",
                        exception,
                        typeof(TimeZoneInfoExtended));
                }
            }
        }
    }
}
