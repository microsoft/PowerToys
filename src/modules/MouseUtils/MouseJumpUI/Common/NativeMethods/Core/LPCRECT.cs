// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;

namespace MouseJumpUI.Common.NativeMethods;

internal static partial class Core
{
    internal readonly struct LPCRECT
    {
        public static readonly LPCRECT Null = new(IntPtr.Zero);

        public readonly IntPtr Value;

        public LPCRECT(IntPtr value)
        {
            this.Value = value;
        }

        public LPCRECT(CRECT value)
        {
            this.Value = LPCRECT.ToPtr(value);
        }

        public bool IsNull => this.Value == LPCRECT.Null.Value;

        private static IntPtr ToPtr(CRECT value)
        {
            var ptr = Marshal.AllocHGlobal(CRECT.Size);
            Marshal.StructureToPtr(value, ptr, false);
            return ptr;
        }

        public void Free()
        {
            Marshal.FreeHGlobal(this.Value);
        }

        public static implicit operator IntPtr(LPCRECT value) => value.Value;

        public static explicit operator LPCRECT(IntPtr value) => new(value);

        public override string ToString()
        {
            return $"{this.GetType().Name}({this.Value})";
        }
    }
}
