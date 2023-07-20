// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace MouseJumpUI.NativeMethods;

internal static partial class Core
{
    /// <summary>
    /// A handle to a brush.
    /// This type is declared in WinDef.h as follows:
    /// typedef HANDLE HBRUSH;
    /// </summary>
    /// <remarks>
    /// See https://learn.microsoft.com/en-us/windows/win32/winprog/windows-data-types
    /// </remarks>
    internal readonly struct HBRUSH
    {
        public static readonly HBRUSH Null = new(IntPtr.Zero);

        public readonly IntPtr Value;

        public HBRUSH(IntPtr value)
        {
            this.Value = value;
        }

        public bool IsNull => this.Value == HBRUSH.Null.Value;

        public static implicit operator IntPtr(HBRUSH value) => value.Value;

        public static explicit operator HBRUSH(IntPtr value) => new(value);

        public override string ToString()
        {
            return $"{this.GetType().Name}({this.Value})";
        }
    }
}
