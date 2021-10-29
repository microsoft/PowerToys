// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Microsoft.PowerToys.Run.Plugin.TimeZone.Classes
{
    public sealed class OneTimeZone
    {
        public OneTimeZone()
        {
            Name = string.Empty;
            MilitaryName = string.Empty;
            Shortcut = string.Empty;

            TimeNamesStandard = Enumerable.Empty<string>();
            TimeNamesDaylight = Enumerable.Empty<string>();
            CountriesStandard = Enumerable.Empty<string>();
            CountriesDaylight = Enumerable.Empty<string>();
        }

        /// <summary>
        /// Gets or sets the time offset of this time zone (the gap from the UTC time zone)
        /// </summary>
        public TimeSpan Offset { get; set; }

        /// <summary>
        /// Gets or sets the name of this time zone.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the military name of this time zone.
        /// </summary>
        public string MilitaryName { get; set; }

        /// <summary>
        /// Gets or sets the shortcuts of the name this time zone.
        /// </summary>
        public string Shortcut { get; set; }

        /// <summary>
        /// Gets or sets a list with names for the standard time.
        /// </summary>
        public IEnumerable<string> TimeNamesStandard { get; set; }

        /// <summary>
        /// Gets or sets a list with names for the daylight saving time.
        /// </summary>
        public IEnumerable<string> TimeNamesDaylight { get; set; }

        /// <summary>
        /// Gets or sets a list with all countries in this time zone that don't use a daylight saving time.
        /// </summary>
        public IEnumerable<string> CountriesStandard { get; set; }

        /// <summary>
        /// Gets or sets a list with all countries in this time zone that use a daylight saving time.
        /// </summary>
        public IEnumerable<string> CountriesDaylight { get; set; }

        internal string OffsetString => Offset.ToString("-hh:mm", CultureInfo.InvariantCulture);
    }
}
