// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;

namespace PowerDisplay.Common.Drivers
{
    /// <summary>
    /// Display configuration mode information union
    /// </summary>
    [StructLayout(LayoutKind.Explicit)]
    public struct DisplayConfigModeInfoUnion
    {
        [FieldOffset(0)]
        public DisplayConfigTargetMode TargetMode;

        [FieldOffset(0)]
        public DisplayConfigSourceMode SourceMode;
    }
}
