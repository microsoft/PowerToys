// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace MouseJumpUI.Common.NativeMethods;

internal static partial class Core
{
    /// <summary>
    /// A handle to a device context (DC).
    /// This type is declared in WinDef.h as follows:
    /// typedef HANDLE HDC;
    /// </summary>
    /// <remarks>
    /// See https://learn.microsoft.com/en-us/windows/win32/winprog/windows-data-types
    /// </remarks>
    internal readonly struct HDC
    {
        public static readonly HDC Null = new(IntPtr.Zero);

        public readonly IntPtr Value;

        public HDC(IntPtr value)
        {
            this.Value = value;
        }

        public bool IsNull => this.Value == HDC.Null.Value;

        public static implicit operator IntPtr(HDC value) => value.Value;

        public static explicit operator HDC(IntPtr value) => new(value);

        public override string ToString()
        {
            return $"{this.GetType().Name}({this.Value})";
        }
    }
}
