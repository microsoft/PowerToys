// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace MouseJumpUI.NativeMethods;

internal static partial class Core
{
    /// <summary>
    /// A message parameter.
    /// This type is declared in WinDef.h as follows:
    /// typedef UINT_PTR WPARAM;
    /// </summary>
    /// <remarks>
    /// See https://learn.microsoft.com/en-us/windows/win32/winprog/windows-data-types
    /// </remarks>
    internal readonly struct WPARAM
    {
        public static readonly WPARAM Null = new(UIntPtr.Zero);

        public readonly UIntPtr Value;

        public WPARAM(UIntPtr value)
        {
            this.Value = value;
        }

        public static implicit operator UIntPtr(WPARAM value) => value.Value;

        public static explicit operator WPARAM(UIntPtr value) => new(value);

        public override string ToString()
        {
            return $"{this.GetType().Name}({this.Value})";
        }
    }
}
