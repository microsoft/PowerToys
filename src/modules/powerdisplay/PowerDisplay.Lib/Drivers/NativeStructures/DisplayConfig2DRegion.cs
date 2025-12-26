// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;

namespace PowerDisplay.Common.Drivers
{
    /// <summary>
    /// Display configuration 2D region
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct DisplayConfig2DRegion
    {
        public uint Cx;
        public uint Cy;
    }
}
