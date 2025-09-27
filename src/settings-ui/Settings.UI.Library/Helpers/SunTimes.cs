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
        public int SunriseHour;
        public int SunriseMinute;
        public int SunsetHour;
        public int SunsetMinute;
        public string Text;

        public bool HasSunrise;
        public bool HasSunset;
    }
}
