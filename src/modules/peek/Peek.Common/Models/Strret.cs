// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace Peek.Common.Models
{
    [SuppressMessage("Microsoft.Portability", "CA1900:ValueTypeFieldsShouldBePortable", Justification = "Targeting Windows (X86/AMD64/ARM) only")]
    [StructLayout(LayoutKind.Explicit)]
    public struct Strret
    {
        [FieldOffset(0)]
        public int UType;

        [FieldOffset(4)]
        public IntPtr POleStr;

        [FieldOffset(4)]
        public IntPtr PStr;

        [FieldOffset(4)]
        public int UOffset;

        [FieldOffset(4)]
        public IntPtr CStr;
    }
}
