// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace MouseJumpUI.NativeMethods;

internal static partial class Core
{
    /// <summary>
    /// A handle to an instance. This is the base address of the module in memory.
    /// HMODULE and HINSTANCE are the same today, but represented different things in 16-bit Windows.
    /// This type is declared in WinDef.h as follows:
    /// typedef HANDLE HINSTANCE;
    /// </summary>
    /// <remarks>
    /// See https://learn.microsoft.com/en-us/windows/win32/winprog/windows-data-types
    /// </remarks>
    internal readonly struct HINSTANCE
    {
        public static readonly HINSTANCE Null = new(IntPtr.Zero);

        public readonly IntPtr Value;

        public HINSTANCE(IntPtr value)
        {
            this.Value = value;
        }

        public bool IsNull => this.Value == HINSTANCE.Null.Value;

        public static implicit operator IntPtr(HINSTANCE value) => value.Value;

        public static explicit operator HINSTANCE(IntPtr value) => new(value);

        public override string ToString()
        {
            return $"{this.GetType().Name}({this.Value})";
        }
    }
}
