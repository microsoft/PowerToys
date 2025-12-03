// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.PowerToys.Settings.UI.Library.Helpers
{
    public struct SunTimes
    {
        public int SunriseHour { get; set; }

        public int SunriseMinute { get; set; }

        public int SunsetHour { get; set; }

        public int SunsetMinute { get; set; }

        public string Text { get; set; }

        public bool HasSunrise { get; set; }

        public bool HasSunset { get; set; }
    }
}
