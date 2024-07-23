// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace MouseJumpUI.Common.NativeMethods;

internal static partial class Core
{
    /// <summary>
    /// A 32-bit signed integer.The range is -2147483648 through 2147483647 decimal.
    /// This type is declared in WinNT.h as follows:
    /// typedef long LONG;
    /// </summary>
    /// <remarks>
    /// See https://learn.microsoft.com/en-us/windows/win32/winprog/windows-data-types
    /// </remarks>
    internal readonly struct LONG
    {
        public readonly int Value;

        public LONG(int value)
        {
            this.Value = value;
        }

        public static implicit operator int(LONG value) => value.Value;

        public static implicit operator LONG(int value) => new(value);

        public override string ToString()
        {
            return $"{this.GetType().Name}({this.Value})";
        }
    }
}
