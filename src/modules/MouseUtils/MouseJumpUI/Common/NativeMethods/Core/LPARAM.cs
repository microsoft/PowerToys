// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
using System;

namespace MouseJumpUI.Common.NativeMethods;

internal static partial class Core
{
    /// <summary>
    /// A message parameter.
    /// This type is declared in WinDef.h as follows:
    /// typedef LONG_PTR LPARAM;
    /// </summary>
    /// <remarks>
    /// See https://learn.microsoft.com/en-us/windows/win32/winprog/windows-data-types
    /// </remarks>
    internal readonly struct LPARAM
    {
        public static readonly LPARAM Null = new(IntPtr.Zero);

        public readonly IntPtr Value;

        public LPARAM(IntPtr value)
        {
            this.Value = value;
        }

        public bool IsNull => this.Value == LPARAM.Null.Value;

        public static implicit operator IntPtr(LPARAM value) => value.Value;

        public static explicit operator LPARAM(IntPtr value) => new(value);

        public override string ToString()
        {
            return $"{this.GetType().Name}({this.Value})";
        }
    }
}
