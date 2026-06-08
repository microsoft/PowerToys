// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

using static MouseJump.Kicker.NativeMethods.Core;

namespace MouseJump.Kicker.NativeMethods;

internal static partial class Kernel32
{
    [SuppressMessage("SA1304", "SA1304:NonPrivateReadonlyFieldsMustBeginWithUpperCaseLetter", Justification = "Names match Win32 api")]
    [SuppressMessage("SA1307", "SA1307:AccessibleFieldsMustBeginWithUpperCaseLetter", Justification = "Names match Win32 api")]
    [StructLayout(LayoutKind.Sequential)]
    internal readonly struct SECURITY_ATTRIBUTES
    {
        internal readonly DWORD nLength;
        internal readonly LPVOID lpSecurityDescriptor;
        internal readonly BOOL bInheritHandle;

        internal SECURITY_ATTRIBUTES(
            DWORD nLength, LPVOID lpSecurityDescriptor, BOOL bInheritHandle)
        {
            this.nLength = nLength;
            this.lpSecurityDescriptor = lpSecurityDescriptor;
            this.bInheritHandle = bInheritHandle;
        }

        public static int Size =>
            Marshal.SizeOf(typeof(SECURITY_ATTRIBUTES));
    }
}
