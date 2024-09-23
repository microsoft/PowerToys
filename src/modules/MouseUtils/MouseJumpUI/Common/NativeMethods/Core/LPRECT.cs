// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
using System;
using System.Runtime.InteropServices;

namespace MouseJumpUI.Common.NativeMethods;

internal static partial class Core
{
    internal readonly struct LPRECT
    {
        public static readonly LPRECT Null = new(IntPtr.Zero);

        public readonly IntPtr Value;

        public LPRECT(IntPtr value)
        {
            this.Value = value;
        }

        public LPRECT(RECT value)
        {
            this.Value = LPRECT.ToPtr(value);
        }

        public bool IsNull => this.Value == LPRECT.Null.Value;

        private static IntPtr ToPtr(RECT value)
        {
            var ptr = Marshal.AllocHGlobal(RECT.Size);
            Marshal.StructureToPtr(value, ptr, false);
            return ptr;
        }

        public void Free()
        {
            Marshal.FreeHGlobal(this.Value);
        }

        public static implicit operator IntPtr(LPRECT value) => value.Value;

        public static explicit operator LPRECT(IntPtr value) => new(value);

        public override string ToString()
        {
            return $"{this.GetType().Name}({this.Value})";
        }
    }
}
