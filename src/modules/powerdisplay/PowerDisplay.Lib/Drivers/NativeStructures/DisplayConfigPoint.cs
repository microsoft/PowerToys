// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;

namespace PowerDisplay.Common.Drivers
{
    /// <summary>
    /// Display configuration point
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct DisplayConfigPoint
    {
        public int X;
        public int Y;
    }
}
