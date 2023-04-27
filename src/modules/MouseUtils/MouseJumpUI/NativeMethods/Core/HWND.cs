// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace MouseJumpUI.NativeMethods;

internal static partial class Core
{
    /// <summary>
    /// A handle to a window.
    /// This type is declared in WinDef.h as follows:
    /// typedef HANDLE HWND;
    /// </summary>
    /// <remarks>
    /// See https://learn.microsoft.com/en-us/windows/win32/winprog/windows-data-types
    /// </remarks>
    internal readonly struct HWND
    {
        public static readonly HWND Null = new(IntPtr.Zero);

        public readonly IntPtr Value;

        public HWND(IntPtr value)
        {
            this.Value = value;
        }

        public bool IsNull => this.Value == HWND.Null.Value;

        public override string ToString()
        {
            return $"{this.GetType().Name}({this.Value})";
        }
    }
}
