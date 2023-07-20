// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;

namespace MouseJumpUI.NativeMethods;

internal static partial class User32
{
    /// <remarks>
    /// See https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getmessagew
    /// </remarks>
    internal readonly struct LPMSG
    {
        public static readonly LPMSG Null = new(IntPtr.Zero);

        public readonly IntPtr Value;

        public LPMSG(IntPtr value)
        {
            this.Value = value;
        }

        public LPMSG(MSG value)
        {
            this.Value = LPMSG.ToPtr(value);
        }

        public MSG ToStructure()
        {
            return Marshal.PtrToStructure<MSG>(this.Value);
        }

        private static IntPtr ToPtr(MSG value)
        {
            var ptr = Marshal.AllocHGlobal(MSG.Size);
            Marshal.StructureToPtr(value, ptr, false);
            return ptr;
        }

        public void Free()
        {
            Marshal.FreeHGlobal(this.Value);
        }

        public override string ToString()
        {
            return $"{this.GetType().Name}({this.Value})";
        }
    }
}
