// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using static PowerDisplay.Common.Drivers.PInvoke;

namespace PowerDisplay.Common.Drivers.DDC
{
    /// <summary>
    /// The production <see cref="IVcpFeatureReader"/>: one <c>GetVCPFeatureAndVCPFeatureReply</c>
    /// transaction, with no retry, pacing or logging. Those belong to the callers — see
    /// <see cref="VcpFeatureProbeService"/>.
    /// </summary>
    internal sealed class NativeVcpFeatureReader : IVcpFeatureReader
    {
        public VcpReadAttempt Read(IntPtr handle, byte code) =>
            GetVCPFeatureAndVCPFeatureReply(handle, code, IntPtr.Zero, out uint current, out uint maximum)
                ? VcpReadAttempt.Success(current, maximum)
                : VcpReadAttempt.Failure(Marshal.GetLastWin32Error());
    }
}
