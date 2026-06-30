// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace PowerDisplay.Common.Models
{
    /// <summary>
    /// Logical monitor features that may map to one of several candidate VCP codes.
    /// </summary>
    public enum VcpFeature
    {
        Brightness,
        Contrast,
        Volume,
        ColorTemperature,
        InputSource,
        PowerState,
    }
}
