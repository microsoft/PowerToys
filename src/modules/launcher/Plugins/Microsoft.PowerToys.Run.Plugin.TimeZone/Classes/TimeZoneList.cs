// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Linq;

namespace Microsoft.PowerToys.Run.Plugin.TimeZone.Classes
{
    public sealed class TimeZoneList
    {
        public TimeZoneList()
        {
            TimeZones = Enumerable.Empty<OneTimeZone>();
        }

        public IEnumerable<OneTimeZone> TimeZones { get; set; }
    }
}
