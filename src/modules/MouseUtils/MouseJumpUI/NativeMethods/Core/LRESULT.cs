// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace MouseJumpUI.NativeMethods;

internal static partial class Core
{
    /// <summary>
    /// Signed result of message processing.
    /// This type is declared in WinDef.h as follows:
    /// typedef LONG_PTR LRESULT;
    /// </summary>
    /// <remarks>
    /// See https://learn.microsoft.com/en-us/windows/win32/winprog/windows-data-types
    /// </remarks>
    internal readonly struct LRESULT
    {
        public static readonly LRESULT Null = new(IntPtr.Zero);

        public readonly IntPtr Value;

        public LRESULT(IntPtr value)
        {
            this.Value = value;
        }

        public static implicit operator IntPtr(LRESULT value) => value.Value;

        public static explicit operator LRESULT(IntPtr value) => new(value);

        public override string ToString()
        {
            return $"{this.GetType().Name}({this.Value})";
        }
    }
}
