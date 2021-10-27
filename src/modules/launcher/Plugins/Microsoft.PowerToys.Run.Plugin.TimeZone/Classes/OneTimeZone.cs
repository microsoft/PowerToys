// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Linq;

namespace Microsoft.PowerToys.Run.Plugin.TimeZone.Classes
{
    public sealed class OneTimeZone
    {
        public OneTimeZone()
        {
            Offset = "0:00";
            Shortcut = string.Empty;
            Names = Enumerable.Empty<string>();
            Countries = Enumerable.Empty<string>();
            DstCountries = Enumerable.Empty<string>();
        }

        /// <summary>
        /// Gets or sets the time offset of this timezone (the gap from the UTC timezone)
        /// </summary>
        public string Offset { get; set; }

        /// <summary>
        /// Gets or sets a list with names of this time zone.
        /// </summary>
        public IEnumerable<string> Names { get; set; }

        public string Shortcut { get; set; }

        /// <summary>
        /// Gets or sets a list with all countries in this time zone that don't use a daylight saving time.
        /// </summary>
        public IEnumerable<string> Countries { get; set; }

        /// <summary>
        /// Gets or sets a list with all countries in this time zone that use a daylight saving time.
        /// </summary>
        public IEnumerable<string> DstCountries { get; set; }
    }
}
