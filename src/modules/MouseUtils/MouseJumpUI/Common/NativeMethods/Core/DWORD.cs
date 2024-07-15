// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;

namespace MouseJumpUI.Common.NativeMethods;

internal static partial class Core
{
    /// <summary>
    /// A 32-bit unsigned integer. The range is 0 through 4294967295 decimal.
    /// This type is declared in IntSafe.h as follows:
    /// typedef unsigned long DWORD;
    /// </summary>
    /// <remarks>
    /// See https://learn.microsoft.com/en-us/windows/win32/winprog/windows-data-types
    /// </remarks>
    internal readonly struct DWORD
    {
        public readonly uint Value;

        public DWORD(uint value)
        {
            this.Value = value;
        }

        public static int Size =>
            Marshal.SizeOf(typeof(DWORD));

        public static implicit operator uint(DWORD value) => value.Value;

        public static implicit operator DWORD(uint value) => new(value);

        public static explicit operator int(DWORD value) => (int)value.Value;

        public static explicit operator DWORD(int value) => new((uint)value);

        public override string ToString()
        {
            return $"{this.GetType().Name}({this.Value})";
        }
    }
}
