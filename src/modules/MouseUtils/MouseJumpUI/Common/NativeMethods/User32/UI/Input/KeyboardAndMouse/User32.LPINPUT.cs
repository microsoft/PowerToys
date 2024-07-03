// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace MouseJumpUI.Common.NativeMethods;

internal static partial class User32
{
    /// <remarks>
    /// See https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-sendinput
    /// </remarks>
    internal readonly struct LPINPUT
    {
        public static readonly LPINPUT Null = new(IntPtr.Zero);

        public readonly IntPtr Value;

        public LPINPUT(IntPtr value)
        {
            this.Value = value;
        }

        public LPINPUT(INPUT[] values)
        {
            this.Value = LPINPUT.ToPtr(values);
        }

        public INPUT ToStructure()
        {
            return Marshal.PtrToStructure<INPUT>(this.Value);
        }

        public IEnumerable<INPUT> ToStructure(int count)
        {
            var ptr = this.Value;
            var size = INPUT.Size;
            for (var i = 0; i < count; i++)
            {
                yield return Marshal.PtrToStructure<INPUT>(this.Value);
                ptr += size;
            }
        }

        private static IntPtr ToPtr(INPUT[] values)
        {
            var mem = Marshal.AllocHGlobal(INPUT.Size * values.Length);
            var ptr = mem;
            var size = INPUT.Size;
            foreach (var value in values)
            {
                Marshal.StructureToPtr(value, ptr, false);
                ptr += size;
            }

            return mem;
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
