// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace MouseJumpUI.Common.NativeMethods;

internal static partial class Core
{
    /// <summary>
    /// A handle to an object.
    /// This type is declared in WinNT.h as follows:
    /// typedef PVOID HANDLE;
    /// </summary>
    /// <remarks>
    /// See https://learn.microsoft.com/en-us/windows/win32/winprog/windows-data-types
    /// </remarks>
    internal readonly struct HANDLE
    {
        public static readonly HANDLE Null = new(IntPtr.Zero);

        public readonly IntPtr Value;

        public HANDLE(IntPtr value)
        {
            this.Value = value;
        }

        public bool IsNull => this.Value == HANDLE.Null.Value;

        public static implicit operator IntPtr(HANDLE value) => value.Value;

        public static explicit operator HANDLE(IntPtr value) => new(value);

        public override string ToString()
        {
            return $"{this.GetType().Name}({this.Value})";
        }
    }
}
