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
            Name = string.Empty;
            Shortcut = string.Empty;
            Countries = Enumerable.Empty<string>();
        }

        public string Offset { get; set; }

        public string Shortcut { get; set; }

        public bool DaylightSavingTime { get; set; }

        public string Name { get; set; }

        public IEnumerable<string> Countries { get; set; }
    }
}
