// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace MouseJumpUI.Common.NativeMethods;

internal static partial class Core
{
    /// <summary>
    /// An unsigned LONG_PTR.
    /// This type is declared in BaseTsd.h as follows:
    /// C++
    /// #if defined(_WIN64)
    ///  typedef unsigned __int64 ULONG_PTR;
    /// #else
    ///  typedef unsigned long ULONG_PTR;
    /// #endif
    /// </summary>
    /// <remarks>
    /// See https://learn.microsoft.com/en-us/windows/win32/winprog/windows-data-types
    /// </remarks>
    internal readonly struct ULONG_PTR
    {
        public static readonly ULONG_PTR Null = new(UIntPtr.Zero);

        public readonly UIntPtr Value;

        public ULONG_PTR(UIntPtr value)
        {
            this.Value = value;
        }

        public static implicit operator UIntPtr(ULONG_PTR value) => value.Value;

        public static explicit operator ULONG_PTR(UIntPtr value) => new(value);

        public override string ToString()
        {
            return $"{this.GetType().Name}({this.Value})";
        }
    }
}
