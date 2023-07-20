// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;

namespace MouseJumpUI.NativeMethods;

internal static partial class Core
{
    /// <summary>
    /// A pointer to any type.
    /// This type is declared in WinDef.h as follows:
    /// typedef void* LPVOID;
    /// </summary>
    /// <remarks>
    /// See https://learn.microsoft.com/en-us/windows/win32/winprog/windows-data-types
    /// </remarks>
    internal readonly struct LPVOID
    {
        public static readonly LPVOID Null = new(IntPtr.Zero);

        public readonly IntPtr Value;

        public LPVOID(IntPtr value)
        {
            this.Value = value;
        }

        public static implicit operator IntPtr(LPVOID value) => value.Value;

        public static explicit operator LPVOID(IntPtr value) => new(value);

        public static LPVOID Allocate(int length)
        {
            var ptr = Marshal.AllocHGlobal(length);
            return new LPVOID(ptr);
        }

        public string? PtrToStringUni()
        {
            return Marshal.PtrToStringUni(this.Value);
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
