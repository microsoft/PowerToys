// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace Microsoft.PowerToys.Run.Plugin.TimeZone.Classes
{
    /// <summary>
    /// A class that contains all time zones.
    /// </summary>
    public sealed class TimeZoneList
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TimeZoneList"/> class with empty properties.
        /// </summary>
        /// <remarks>
        /// The standard constructor is need by the <see cref="JsonSerializer.Deserialize{TValue}(string, JsonSerializerOptions?)"/>-Method.
        /// </remarks>
        public TimeZoneList()
        {
            TimeZones = Enumerable.Empty<TimeZoneProperties>();
        }

        /// <summary>
        /// Gets or sets a list with all time zones.
        /// </summary>
        public IEnumerable<TimeZoneProperties> TimeZones { get; set; }
    }
}
