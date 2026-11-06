// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace PowerDisplay.Common.Models
{
    [Flags]
    public enum MonitorReadFlags
    {
        None = 0,
        Brightness = 1 << 0,
        Contrast = 1 << 1,
        Volume = 1 << 2,
        ColorTemperature = 1 << 3,
        InputSource = 1 << 4,
        PowerState = 1 << 5,
        Orientation = 1 << 6,
    }
}
