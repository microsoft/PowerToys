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
            Shortcuts = Enumerable.Empty<string>();
            Names = Enumerable.Empty<string>();
            Countries = Enumerable.Empty<string>();
            DstCountries = Enumerable.Empty<string>();
        }

        /// <summary>
        /// Gets or sets the time offset of this timezone (the gap from the UTC timezone)
        /// </summary>
        public TimeSpan Offset { get; set; }

        /// <summary>
        /// Gets or sets a list with names of this time zone.
        /// </summary>
        public IEnumerable<string> Names { get; set; }

        /// <summary>
        /// Gets or sets a list with shortcuts of the names this time zone.
        /// </summary>
        public IEnumerable<string> Shortcuts { get; set; }

        /// <summary>
        /// Gets or sets a list with all countries in this time zone that don't use a daylight saving time.
        /// </summary>
        public IEnumerable<string> Countries { get; set; }

        /// <summary>
        /// Gets or sets a list with all countries in this time zone that use a daylight saving time.
        /// </summary>
        public IEnumerable<string> DstCountries { get; set; }

        internal string OffsetString => Offset.ToString("-hh:mm", CultureInfo.InvariantCulture);
    }
}
