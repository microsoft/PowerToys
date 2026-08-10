// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;

namespace MouseJump.Kicker.NativeMethods;

internal static partial class Kernel32
{
    internal readonly struct LPSECURITY_ATTRIBUTES
    {
        public static readonly LPSECURITY_ATTRIBUTES Null = new(IntPtr.Zero);

        public readonly IntPtr Value;

        public LPSECURITY_ATTRIBUTES(IntPtr value)
        {
            this.Value = value;
        }

        public LPSECURITY_ATTRIBUTES(SECURITY_ATTRIBUTES value)
        {
            this.Value = LPSECURITY_ATTRIBUTES.ToPtr(value);
        }

        public bool IsNull => this.Value == LPSECURITY_ATTRIBUTES.Null.Value;

        private static IntPtr ToPtr(SECURITY_ATTRIBUTES value)
        {
            var ptr = Marshal.AllocHGlobal(SECURITY_ATTRIBUTES.Size);
            Marshal.StructureToPtr(value, ptr, false);
            return ptr;
        }

        public SECURITY_ATTRIBUTES ToStructure()
        {
            return Marshal.PtrToStructure<SECURITY_ATTRIBUTES>(this.Value);
        }

        public void Free()
        {
            Marshal.FreeHGlobal(this.Value);
        }

        public static implicit operator IntPtr(LPSECURITY_ATTRIBUTES value) => value.Value;

        public static implicit operator LPSECURITY_ATTRIBUTES(IntPtr value) => new(value);

        public override string ToString()
        {
            return $"{this.GetType().Name}({this.Value})";
        }
    }
}
